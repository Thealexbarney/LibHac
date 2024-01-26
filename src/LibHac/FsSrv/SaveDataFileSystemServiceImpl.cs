using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Util;
using Utility = LibHac.FsSrv.Impl.Utility;

namespace LibHac.FsSrv;

/// <summary>
/// Handles the lower-level operations on save data.
/// <see cref="SaveDataFileSystemService"/> uses this class to provide save data APIs at a higher level of abstraction.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class SaveDataFileSystemServiceImpl : IDisposable
{
    private static readonly bool UseTargetManager = true;

    private Configuration _config;
    private EncryptionSeed _encryptionSeed;

    private SaveDataFileSystemCacheManager _saveFileSystemCacheManager;
    private SaveDataExtraDataAccessorCacheManager _saveExtraDataCacheManager;
    // Save data porter manager
    private bool _isSdCardAccessible;
    private TimeStampGetter _timeStampGetter;

    internal HorizonClient Hos => _config.FsServer.Hos;
    internal FileSystemServer FsServer => _config.FsServer;

    private class TimeStampGetter : ISaveDataCommitTimeStampGetter
    {
        private SaveDataFileSystemServiceImpl _saveService;

        public TimeStampGetter(SaveDataFileSystemServiceImpl saveService)
        {
            _saveService = saveService;
        }

        public Result Get(out long timeStamp)
        {
            return _saveService.GetSaveDataCommitTimeStamp(out timeStamp).Ret();
        }
    }

    public struct Configuration
    {
        public BaseFileSystemServiceImpl BaseFsService;
        public TimeServiceImpl TimeService;
        public ILocalFileSystemCreator LocalFsCreator;
        public ITargetManagerFileSystemCreator TargetManagerFsCreator;
        public ISaveDataFileSystemCreator SaveFsCreator;
        public IEncryptedFileSystemCreator EncryptedFsCreator;
        public ProgramRegistryServiceImpl ProgramRegistryService;
        public IBufferManager BufferManager;
        public RandomDataGenerator GenerateRandomData;
        public SaveDataTransferCryptoConfiguration SaveTransferCryptoConfig;
        public int SaveDataFileSystemCacheCount;
        public Func<bool> IsPseudoSaveData;
        public ISaveDataIndexerManager SaveIndexerManager;
        public DebugConfigurationServiceImpl DebugConfigService;

        // LibHac additions
        public FileSystemServer FsServer;
    }

    private static bool IsDeviceUniqueMac(SaveDataSpaceId spaceId)
    {
        return spaceId == SaveDataSpaceId.System ||
               spaceId == SaveDataSpaceId.User ||
               spaceId == SaveDataSpaceId.Temporary ||
               spaceId == SaveDataSpaceId.ProperSystem ||
               spaceId == SaveDataSpaceId.SafeMode;
    }

    private static Result WipeData(IFileSystem fileSystem, ref readonly Path filePath, RandomDataGenerator random)
    {
        throw new NotImplementedException();
    }

    private static Result WipeMasterHeader(IFileSystem fileSystem, ref readonly Path filePath, RandomDataGenerator random)
    {
        throw new NotImplementedException();
    }

    public SaveDataFileSystemServiceImpl(in Configuration configuration)
    {
        _config = configuration;
        _saveFileSystemCacheManager = new SaveDataFileSystemCacheManager();
        _saveExtraDataCacheManager = new SaveDataExtraDataAccessorCacheManager();

        _timeStampGetter = new TimeStampGetter(this);

        Result res = _saveFileSystemCacheManager.Initialize(_config.SaveDataFileSystemCacheCount);
        Abort.DoAbortUnless(res.IsSuccess());
    }

    public void Dispose()
    {
        _saveFileSystemCacheManager.Dispose();
        _saveExtraDataCacheManager.Dispose();
    }

    public DebugConfigurationServiceImpl GetDebugConfigurationService()
    {
        return _config.DebugConfigService;
    }

    public Result DoesSaveDataEntityExist(out bool exists, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        UnsafeHelpers.SkipParamInit(out exists);

        using var fileSystem = new SharedRef<IFileSystem>();

        Result res = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId);
        if (res.IsFailure()) return res.Miss();

        // Get the path of the save data
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        using scoped var saveImageName = new Path();
        res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.Get.GetEntryType(out _, in saveImageName);

        if (res.IsSuccess())
        {
            exists = true;
            return Result.Success;
        }
        else if (ResultFs.PathNotFound.Includes(res))
        {
            exists = false;
            return Result.Success;
        }
        else
        {
            return res.Miss();
        }
    }

    public Result OpenSaveDataFile(ref SharedRef<IFile> outFile, SaveDataSpaceId spaceId, ulong saveDataId,
        OpenMode openMode)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataFileSystem(ref SharedRef<IFileSystem> outFileSystem, SaveDataSpaceId spaceId,
        ulong saveDataId, ref readonly Path saveDataRootPath, bool openReadOnly, SaveDataType type, bool cacheExtraData)
    {
        using var fileSystem = new SharedRef<IFileSystem>();

        Result res = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId, in saveDataRootPath, true);
        if (res.IsFailure()) return res.Miss();

        bool isEmulatedOnHost = IsAllowedDirectorySaveData(spaceId, in saveDataRootPath);

        if (isEmulatedOnHost)
        {
            // Create the save data directory on the host if needed.
            Unsafe.SkipInit(out Array18<byte> saveDirectoryNameBuffer);
            using scoped var saveDirectoryName = new Path();
            res = PathFunctions.SetUpFixedPathSaveId(ref saveDirectoryName.Ref(), saveDirectoryNameBuffer, saveDataId);
            if (res.IsFailure()) return res.Miss();

            res = FsSystem.Utility.EnsureDirectory(fileSystem.Get, in saveDirectoryName);
            if (res.IsFailure()) return res.Miss();
        }

        using var saveDataFs = new SharedRef<ISaveDataFileSystem>();

        using (_saveFileSystemCacheManager.GetScopedLock())
        using (_saveExtraDataCacheManager.GetScopedLock())
        {
            if (isEmulatedOnHost || !_saveFileSystemCacheManager.GetCache(ref saveDataFs.Ref, spaceId, saveDataId))
            {
                bool isDeviceUniqueMac = IsDeviceUniqueMac(spaceId);
                bool isJournalingSupported = SaveDataProperties.IsJournalingSupported(type);
                bool isMultiCommitSupported = SaveDataProperties.IsMultiCommitSupported(type);
                bool openShared = SaveDataProperties.IsSharedOpenNeeded(type);
                bool isReconstructible = SaveDataProperties.IsReconstructible(type, spaceId);

                res = _config.SaveFsCreator.Create(ref saveDataFs.Ref, ref fileSystem.Ref, spaceId, saveDataId,
                    isEmulatedOnHost, isDeviceUniqueMac, isJournalingSupported, isMultiCommitSupported,
                    openReadOnly, openShared, _timeStampGetter, isReconstructible);
                if (res.IsFailure()) return res.Miss();
            }

            if (!isEmulatedOnHost && cacheExtraData)
            {
                using SharedRef<ISaveDataExtraDataAccessor> extraDataAccessor =
                    SharedRef<ISaveDataExtraDataAccessor>.CreateCopy(in saveDataFs);

                res = _saveExtraDataCacheManager.Register(in extraDataAccessor, spaceId, saveDataId);
                if (res.IsFailure()) return res.Miss();
            }
        }

        using var registerFs = new SharedRef<SaveDataFileSystemCacheRegister>(
            new SaveDataFileSystemCacheRegister(ref saveDataFs.Ref, _saveFileSystemCacheManager, spaceId, saveDataId));

        if (openReadOnly)
        {
            using SharedRef<IFileSystem> tempFs = SharedRef<IFileSystem>.CreateMove(ref registerFs.Ref);
            using var readOnlyFileSystem = new SharedRef<ReadOnlyFileSystem>(new ReadOnlyFileSystem(ref tempFs.Ref));

            if (!readOnlyFileSystem.HasValue)
                return ResultFs.AllocationMemoryFailedInSaveDataFileSystemServiceImplB.Log();

            outFileSystem.SetByMove(ref readOnlyFileSystem.Ref);
        }
        else
        {
            outFileSystem.SetByMove(ref registerFs.Ref);
        }

        return Result.Success;
    }

    public Result OpenSaveDataMetaDirectoryFileSystem(ref SharedRef<IFileSystem> outFileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Unsafe.SkipInit(out Array27<byte> saveDataMetaIdDirectoryNameBuffer);

        using scoped var saveDataMetaIdDirectoryName = new Path();
        Result res = PathFunctions.SetUpFixedPathSaveMetaDir(ref saveDataMetaIdDirectoryName.Ref(),
            saveDataMetaIdDirectoryNameBuffer, saveDataId);
        if (res.IsFailure()) return res.Miss();

        return OpenSaveDataDirectoryFileSystemImpl(ref outFileSystem, spaceId, in saveDataMetaIdDirectoryName).Ret();
    }

    public Result OpenSaveDataInternalStorageFileSystem(ref SharedRef<IFileSystem> outFileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId, ref readonly Path saveDataRootPath, bool useSecondMacKey,
        bool isReconstructible)
    {
        throw new NotImplementedException();
    }

    private Result OpenSaveDataImageFile(ref UniqueRef<IFile> outFile, SaveDataSpaceId spaceId, ulong saveDataId,
        ref readonly Path saveDataRootPath)
    {
        throw new NotImplementedException();
    }

    public Result ExtendSaveDataFileSystemCore(out long extendedTotalSize, ulong saveDataId, SaveDataSpaceId spaceId,
        SaveDataType type, long dataSize, long journalSize, ref readonly Path saveDataRootPath, bool isExtensionStart)
    {
        throw new NotImplementedException();
    }

    public Result StartExtendSaveDataFileSystem(out long extendedTotalSize, ulong saveDataId, SaveDataSpaceId spaceId,
        SaveDataType type, long dataSize, long journalSize, ref readonly Path saveDataRootPath)
    {
        return ExtendSaveDataFileSystemCore(out extendedTotalSize, saveDataId, spaceId, type, dataSize, journalSize,
            in saveDataRootPath, isExtensionStart: true);
    }

    public Result ResumeExtendSaveDataFileSystem(out long extendedTotalSize, ulong saveDataId, SaveDataSpaceId spaceId,
        SaveDataType type, ref readonly Path saveDataRootPath)
    {
        return ExtendSaveDataFileSystemCore(out extendedTotalSize, saveDataId, spaceId, type, dataSize: 0,
            journalSize: 0, in saveDataRootPath, isExtensionStart: false);
    }

    public Result FinishExtendSaveDataFileSystem(ulong saveDataId, SaveDataSpaceId spaceId)
    {
        Result res = DeleteSaveDataMeta(saveDataId, spaceId, SaveDataMetaType.ExtensionContext);
        if (res.IsFailure() && !ResultFs.PathNotFound.Includes(res))
            return res.Miss();

        return Result.Success;
    }

    public void RevertExtendSaveDataFileSystem(ulong saveDataId, SaveDataSpaceId spaceId, long originalSize,
        ref readonly Path saveDataRootPath)
    {
        using var saveDataFile = new UniqueRef<IFile>();
        Result res = OpenSaveDataImageFile(ref saveDataFile.Ref, spaceId, saveDataId, in saveDataRootPath);

        if (res.IsSuccess())
        {
            saveDataFile.Get.SetSize(originalSize).IgnoreResult();
        }

        FinishExtendSaveDataFileSystem(saveDataId, spaceId).IgnoreResult();
    }

    public Result QuerySaveDataTotalSize(out long totalSize, int blockSize, long dataSize, long journalSize)
    {
        // Todo: Implement
        totalSize = 0;
        return Result.Success;
    }

    public Result CreateSaveDataMeta(ulong saveDataId, SaveDataSpaceId spaceId, SaveDataMetaType metaType,
        long metaFileSize)
    {
        using var fileSystem = new SharedRef<IFileSystem>();

        Result res = OpenSaveDataMetaDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId);
        if (res.IsFailure()) return res.Miss();

        Unsafe.SkipInit(out Array15<byte> saveDataMetaNameBuffer);

        using scoped var saveDataMetaName = new Path();
        res = PathFunctions.SetUpFixedPathSaveMetaName(ref saveDataMetaName.Ref(), saveDataMetaNameBuffer,
            (uint)metaType);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.Get.CreateFile(in saveDataMetaName, metaFileSize);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result DeleteSaveDataMeta(ulong saveDataId, SaveDataSpaceId spaceId, SaveDataMetaType metaType)
    {
        using var fileSystem = new SharedRef<IFileSystem>();

        Result res = OpenSaveDataMetaDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId);
        if (res.IsFailure()) return res.Miss();

        Unsafe.SkipInit(out Array15<byte> saveDataMetaNameBuffer);

        using scoped var saveDataMetaName = new Path();
        res = PathFunctions.SetUpFixedPathSaveMetaName(ref saveDataMetaName.Ref(), saveDataMetaNameBuffer,
            (uint)metaType);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.Get.DeleteFile(in saveDataMetaName);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result DeleteAllSaveDataMetas(ulong saveDataId, SaveDataSpaceId spaceId)
    {
        ReadOnlySpan<byte> metaDirName = "/saveMeta"u8;

        Unsafe.SkipInit(out Array18<byte> saveDataIdDirectoryNameBuffer);

        using var fileSystem = new SharedRef<IFileSystem>();

        using scoped var saveDataMetaDirectoryName = new Path();
        Result res = PathFunctions.SetUpFixedPath(ref saveDataMetaDirectoryName.Ref(), metaDirName);
        if (res.IsFailure()) return res.Miss();

        res = OpenSaveDataDirectoryFileSystemImpl(ref fileSystem.Ref, spaceId, in saveDataMetaDirectoryName,
            createIfMissing: false);
        if (res.IsFailure()) return res.Miss();

        using scoped var saveDataIdDirectoryName = new Path();
        PathFunctions.SetUpFixedPathSaveId(ref saveDataIdDirectoryName.Ref(), saveDataIdDirectoryNameBuffer,
            saveDataId);
        if (res.IsFailure()) return res.Miss();

        // Delete the save data's meta directory, ignoring the error if the directory is already gone
        res = fileSystem.Get.DeleteDirectoryRecursively(in saveDataIdDirectoryName);

        if (res.IsFailure())
        {
            if (ResultFs.PathNotFound.Includes(res))
                return Result.Success;

            return res.Miss();
        }

        return Result.Success;
    }

    public Result OpenSaveDataMeta(ref UniqueRef<IFile> outMetaFile, ulong saveDataId, SaveDataSpaceId spaceId,
        SaveDataMetaType metaType)
    {
        using var fileSystem = new SharedRef<IFileSystem>();

        Result res = OpenSaveDataMetaDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId);
        if (res.IsFailure()) return res.Miss();

        Unsafe.SkipInit(out Array15<byte> saveDataMetaNameBuffer);

        using scoped var saveDataMetaName = new Path();
        res = PathFunctions.SetUpFixedPathSaveMetaName(ref saveDataMetaName.Ref(), saveDataMetaNameBuffer,
            (uint)metaType);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.Get.OpenFile(ref outMetaFile, in saveDataMetaName, OpenMode.ReadWrite);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result CreateSaveDataFileSystem(ulong saveDataId, in SaveDataCreationInfo2 creationInfo,
        ref readonly Path saveDataRootPath, bool skipFormat)
    {
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        long dataSize = creationInfo.Size;
        long journalSize = creationInfo.JournalSize;
        ulong ownerId = creationInfo.OwnerId;
        SaveDataSpaceId spaceId = creationInfo.SpaceId;
        SaveDataFlags flags = creationInfo.Flags;

        using var fileSystem = new SharedRef<IFileSystem>();

        Result res = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, creationInfo.SpaceId, saveDataId,
            in saveDataRootPath, allowEmulatedSave: false);
        if (res.IsFailure()) return res.Miss();

        using scoped var saveImageName = new Path();
        res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
        if (res.IsFailure()) return res.Miss();

        bool isPseudoSaveFs = _config.IsPseudoSaveData();
        bool isCreationSuccessful = false;

        try
        {
            if (isPseudoSaveFs)
            {
                res = FsSystem.Utility.EnsureDirectory(fileSystem.Get, in saveImageName);
                if (res.IsFailure()) return res.Miss();
            }
            else
            {
                throw new NotImplementedException();
            }

            SaveDataExtraData extraData = default;
            extraData.Attribute = creationInfo.Attribute;
            extraData.OwnerId = ownerId;

            res = GetSaveDataCommitTimeStamp(out extraData.TimeStamp);
            if (res.IsFailure())
                extraData.TimeStamp = 0;

            extraData.CommitId = 0;
            _config.GenerateRandomData(SpanHelpers.AsByteSpan(ref extraData.CommitId));

            extraData.Flags = flags;
            extraData.DataSize = dataSize;
            extraData.JournalSize = journalSize;
            extraData.FormatType = creationInfo.FormatType;

            res = WriteSaveDataFileSystemExtraData(spaceId, saveDataId, in extraData, in saveDataRootPath,
                creationInfo.Attribute.Type, updateTimeStamp: true);
            if (res.IsFailure()) return res.Miss();

            isCreationSuccessful = true;
            return Result.Success;
        }
        finally
        {
            // Delete any created save if something goes wrong.
            if (!isCreationSuccessful)
            {
                if (isPseudoSaveFs)
                {
                    fileSystem.Get.DeleteDirectoryRecursively(in saveImageName).IgnoreResult();
                }
                else
                {
                    fileSystem.Get.DeleteFile(in saveImageName).IgnoreResult();
                }
            }
        }
    }

    public Result DeleteSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, bool wipeSaveFile,
        ref readonly Path saveDataRootPath)
    {
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        using var fileSystem = new SharedRef<IFileSystem>();

        _saveFileSystemCacheManager.Unregister(spaceId, saveDataId);

        // Open the directory containing the save data
        Result res = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId, in saveDataRootPath, false);
        if (res.IsFailure()) return res.Miss();

        using scoped var saveImageName = new Path();
        res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
        if (res.IsFailure()) return res.Miss();

        // Check if the save data is a file or a directory
        res = fileSystem.Get.GetEntryType(out DirectoryEntryType entryType, in saveImageName);
        if (res.IsFailure()) return res.Miss();

        // Delete the save data, wiping the file if needed
        if (entryType == DirectoryEntryType.Directory)
        {
            res = fileSystem.Get.DeleteDirectoryRecursively(in saveImageName);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            if (wipeSaveFile)
            {
                // If we need to wipe the save file, check if the save is encrypted. If it is, we only wipe the master
                // header because it contains the key data needed to decrypt the save.
                bool isDataEncrypted = false;
                if (GetDebugConfigurationService().Get(DebugOptionKey.SaveDataEncryption, 0) != 0)
                {
                    using SharedRef<IFileSystem> tempFileSystem = SharedRef<IFileSystem>.CreateCopy(in fileSystem);
                    res = _config.SaveFsCreator.IsDataEncrypted(out isDataEncrypted, ref tempFileSystem.Ref,
                        saveDataId, _config.BufferManager, IsDeviceUniqueMac(spaceId), isReconstructible: false);

                    if (res.IsFailure())
                        isDataEncrypted = false;
                }

                if (isDataEncrypted)
                {
                    WipeMasterHeader(fileSystem.Get, in saveImageName, _config.GenerateRandomData).IgnoreResult();
                }
                else
                {
                    WipeData(fileSystem.Get, in saveImageName, _config.GenerateRandomData).IgnoreResult();
                }
            }

            res = fileSystem.Get.DeleteFile(in saveImageName);
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public Result ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, SaveDataSpaceId spaceId,
        ulong saveDataId, SaveDataType type, ref readonly Path saveDataRootPath)
    {
        UnsafeHelpers.SkipParamInit(out extraData);

        // Emulated save data on a host device doesn't have extra data.
        if (IsAllowedDirectorySaveData(spaceId, in saveDataRootPath))
        {
            extraData = default;
            return Result.Success;
        }

        using UniqueLockRef<SdkRecursiveMutexType> scopedLockFsCache = _saveFileSystemCacheManager.GetScopedLock();
        using UniqueLockRef<SdkRecursiveMutexType> scopedLockExtraDataCache = _saveExtraDataCacheManager.GetScopedLock();

        using var unusedSaveDataFs = new SharedRef<IFileSystem>();
        using var extraDataAccessor = new SharedRef<ISaveDataExtraDataAccessor>();

        // Try to grab an extra data accessor for the requested save from the cache.
        Result res = _saveExtraDataCacheManager.GetCache(ref extraDataAccessor.Ref, spaceId, saveDataId);

        if (res.IsFailure())
        {
            // Try to open the extra data accessor if it's not in the cache.

            // We won't actually use the returned save data FS.
            // Opening the FS should cache an extra data accessor for it.
            res = OpenSaveDataFileSystem(ref unusedSaveDataFs.Ref, spaceId, saveDataId, in saveDataRootPath,
                openReadOnly: true, type, cacheExtraData: true);
            if (res.IsFailure()) return res.Miss();

            // Try to grab an accessor from the cache again.
            res = _saveExtraDataCacheManager.GetCache(ref extraDataAccessor.Ref, spaceId, saveDataId);
            if (res.IsFailure()) return res.Miss();
        }

        // We successfully got an extra data accessor. Read the extra data from it.
        res = extraDataAccessor.Get.ReadExtraData(out extraData);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result WriteSaveDataFileSystemExtraData(SaveDataSpaceId spaceId, ulong saveDataId,
        in SaveDataExtraData extraData, ref readonly Path saveDataRootPath, SaveDataType type,
        bool updateTimeStamp)
    {
        // Emulated save data on a host device doesn't have extra data.
        if (IsAllowedDirectorySaveData(spaceId, in saveDataRootPath))
        {
            return Result.Success;
        }

        using UniqueLockRef<SdkRecursiveMutexType> scopedLockFsCache = _saveFileSystemCacheManager.GetScopedLock();
        using UniqueLockRef<SdkRecursiveMutexType> scopedLockExtraDataCache = _saveExtraDataCacheManager.GetScopedLock();

        using var unusedSaveDataFs = new SharedRef<IFileSystem>();
        using var extraDataAccessor = new SharedRef<ISaveDataExtraDataAccessor>();

        // Try to grab an extra data accessor for the requested save from the cache.
        Result res = _saveExtraDataCacheManager.GetCache(ref extraDataAccessor.Ref, spaceId, saveDataId);

        if (res.IsFailure())
        {
            // No accessor was found in the cache. Try to open one.

            // We won't actually use the returned save data FS.
            // Opening the FS should cache an extra data accessor for it.
            res = OpenSaveDataFileSystem(ref unusedSaveDataFs.Ref, spaceId, saveDataId, in saveDataRootPath,
                openReadOnly: false, type, cacheExtraData: true);
            if (res.IsFailure()) return res.Miss();

            // Try to grab an accessor from the cache again.
            res = _saveExtraDataCacheManager.GetCache(ref extraDataAccessor.Ref, spaceId, saveDataId);
            if (res.IsFailure()) return res.Miss();
        }

        // We should have a valid accessor if we've reached this point.
        // Write and commit the extra data.
        res = extraDataAccessor.Get.WriteExtraData(in extraData);
        if (res.IsFailure()) return res.Miss();

        res = extraDataAccessor.Get.CommitExtraData(updateTimeStamp);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result CorruptSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long offset,
        ref readonly Path saveDataRootPath)
    {
        throw new NotImplementedException();
    }

    private Result GetSaveDataCommitTimeStamp(out long timeStamp)
    {
        return _config.TimeService.GetCurrentPosixTime(out timeStamp);
    }

    private bool IsSaveEmulated(ref readonly Path saveDataRootPath)
    {
        return !saveDataRootPath.IsEmpty();
    }

    public Result OpenSaveDataDirectoryFileSystem(ref SharedRef<IFileSystem> outFileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId)
    {
        using scoped var rootPath = new Path();

        return OpenSaveDataDirectoryFileSystem(ref outFileSystem, spaceId, saveDataId, in rootPath, allowEmulatedSave: true);
    }

    public Result OpenSaveDataDirectoryFileSystem(ref SharedRef<IFileSystem> outFileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId, ref readonly Path saveDataRootPath, bool allowEmulatedSave)
    {
        Result res;

        if (allowEmulatedSave && IsAllowedDirectorySaveData(spaceId, in saveDataRootPath))
        {
            if (UseTargetManager)
            {
                using (var tmFileSystem = new SharedRef<IFileSystem>())
                {
                    // Ensure the target save data directory exists
                    res = _config.TargetManagerFsCreator.Create(ref tmFileSystem.Ref, in saveDataRootPath,
                        openCaseSensitive: false, ensureRootPathExists: true,
                        pathNotFoundResult: ResultFs.SaveDataRootPathUnavailable.Value);
                    if (res.IsFailure()) return res.Miss();
                }

                using scoped var path = new Path();
                res = path.Initialize(in saveDataRootPath);
                if (res.IsFailure()) return res.Miss();

                res = _config.TargetManagerFsCreator.NormalizeCaseOfPath(out bool isTargetFsCaseSensitive, ref path.Ref());
                if (res.IsFailure()) return res.Miss();

                res = _config.TargetManagerFsCreator.Create(ref outFileSystem, in path, isTargetFsCaseSensitive,
                    ensureRootPathExists: false, pathNotFoundResult: ResultFs.SaveDataRootPathUnavailable.Value);
                if (res.IsFailure()) return res.Miss();
            }
            else
            {
                res = _config.LocalFsCreator.Create(ref outFileSystem, in saveDataRootPath, openCaseSensitive: true,
                    ensureRootPathExists: true, pathNotFoundResult: ResultFs.SaveDataRootPathUnavailable.Value);
                if (res.IsFailure()) return res.Miss();
            }

            return Result.Success;
        }

        using scoped var saveDataAreaDirectoryName = new Path();
        ReadOnlySpan<byte> saveDirName;

        if (spaceId == SaveDataSpaceId.Temporary)
        {
            saveDirName = "/temp"u8;
        }
        else
        {
            saveDirName = "/save"u8;
        }

        res = PathFunctions.SetUpFixedPath(ref saveDataAreaDirectoryName.Ref(), saveDirName);
        if (res.IsFailure()) return res.Miss();

        res = OpenSaveDataDirectoryFileSystemImpl(ref outFileSystem, spaceId, in saveDataAreaDirectoryName);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result OpenSaveDataDirectoryFileSystemImpl(ref SharedRef<IFileSystem> outFileSystem,
        SaveDataSpaceId spaceId, ref readonly Path directoryPath)
    {
        return OpenSaveDataDirectoryFileSystemImpl(ref outFileSystem, spaceId, in directoryPath, createIfMissing: true);
    }

    public Result OpenSaveDataDirectoryFileSystemImpl(ref SharedRef<IFileSystem> outFileSystem,
        SaveDataSpaceId spaceId, ref readonly Path directoryPath, bool createIfMissing)
    {
        using var baseFileSystem = new SharedRef<IFileSystem>();

        switch (spaceId)
        {
            case SaveDataSpaceId.System:
            {
                Result res = _config.BaseFsService.OpenBisFileSystem(ref baseFileSystem.Ref, BisPartitionId.System,
                    caseSensitive: true);
                if (res.IsFailure()) return res.Miss();

                res = Utility.WrapSubDirectory(ref outFileSystem, ref baseFileSystem.Ref, in directoryPath,
                    createIfMissing);
                if (res.IsFailure()) return res.Miss();

                break;
            }

            case SaveDataSpaceId.User:
            case SaveDataSpaceId.Temporary:
            {
                Result res = _config.BaseFsService.OpenBisFileSystem(ref baseFileSystem.Ref, BisPartitionId.User,
                    caseSensitive: true);
                if (res.IsFailure()) return res.Miss();

                res = Utility.WrapSubDirectory(ref outFileSystem, ref baseFileSystem.Ref, in directoryPath,
                    createIfMissing);
                if (res.IsFailure()) return res.Miss();

                break;
            }

            case SaveDataSpaceId.SdSystem:
            case SaveDataSpaceId.SdUser:
            {
                Result res = _config.BaseFsService.OpenSdCardProxyFileSystem(ref baseFileSystem.Ref,
                    openCaseSensitive: true);
                if (res.IsFailure()) return res.Miss();

                Unsafe.SkipInit(out Array64<byte> pathParentBuffer);

                using scoped var pathParent = new Path();
                res = PathFunctions.SetUpFixedPathSingleEntry(ref pathParent.Ref(), pathParentBuffer,
                    CommonDirNames.SdCardNintendoRootDirectoryName);
                if (res.IsFailure()) return res.Miss();

                using scoped var pathSdRoot = new Path();
                res = pathSdRoot.Combine(in pathParent, in directoryPath);
                if (res.IsFailure()) return res.Miss();

                using SharedRef<IFileSystem> tempFileSystem = SharedRef<IFileSystem>.CreateMove(ref baseFileSystem.Ref);

                res = Utility.WrapSubDirectory(ref baseFileSystem.Ref, ref tempFileSystem.Ref, in pathSdRoot, createIfMissing);
                if (res.IsFailure()) return res.Miss();

                res = _config.EncryptedFsCreator.Create(ref outFileSystem, ref baseFileSystem.Ref,
                    IEncryptedFileSystemCreator.KeyId.Save, in _encryptionSeed);
                if (res.IsFailure()) return res.Miss();

                break;
            }

            case SaveDataSpaceId.ProperSystem:
            {
                Result res = _config.BaseFsService.OpenBisFileSystem(ref baseFileSystem.Ref,
                    BisPartitionId.SystemProperPartition, caseSensitive: true);
                if (res.IsFailure()) return res.Miss();

                res = Utility.WrapSubDirectory(ref outFileSystem, ref baseFileSystem.Ref, in directoryPath,
                    createIfMissing);
                if (res.IsFailure()) return res.Miss();

                break;
            }

            case SaveDataSpaceId.SafeMode:
            {
                Result res = _config.BaseFsService.OpenBisFileSystem(ref baseFileSystem.Ref, BisPartitionId.SafeMode,
                    caseSensitive: true);
                if (res.IsFailure()) return res.Miss();

                res = Utility.WrapSubDirectory(ref outFileSystem, ref baseFileSystem.Ref, in directoryPath,
                    createIfMissing);
                if (res.IsFailure()) return res.Miss();

                break;
            }

            default:
                return ResultFs.InvalidArgument.Log();
        }

        return Result.Success;
    }

    public Result IsProvisionallyCommittedSaveData(out bool isProvisionallyCommitted, in SaveDataInfo saveInfo)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Checks if a save is to be stored on a host device.
    /// </summary>
    public bool IsAllowedDirectorySaveData(SaveDataSpaceId spaceId, ref readonly Path saveDataRootPath)
    {
        return spaceId == SaveDataSpaceId.User && IsSaveEmulated(in saveDataRootPath);
    }

    public Result SetSdCardEncryptionSeed(in EncryptionSeed seed)
    {
        _encryptionSeed = seed;

        _config.SaveFsCreator.SetMacGenerationSeed(seed.Value);
        _config.SaveIndexerManager.InvalidateIndexer(SaveDataSpaceId.SdSystem);
        _config.SaveIndexerManager.InvalidateIndexer(SaveDataSpaceId.SdUser);

        return Result.Success;
    }

    public void SetSdCardAccessibility(bool isAccessible)
    {
        _isSdCardAccessible = isAccessible;
    }

    public bool IsSdCardAccessible()
    {
        return _isSdCardAccessible;
    }

    /// <summary>
    /// Gets the program ID of the save data associated with the specified program ID.
    /// </summary>
    /// <remarks>In a standard application the program ID will be the same as the input program ID.
    /// In multi-program applications all sub-programs use the program ID of the main program
    /// for their save data. The main program always has a program index of 0.</remarks>
    /// <param name="programId">The program ID to get the save data program ID for.</param>
    /// <returns>The program ID of the save data.</returns>
    public ProgramId ResolveDefaultSaveDataReferenceProgramId(ProgramId programId)
    {
        // First check if the program ID is part of a multi-program application that contains a program with index 0.
        ProgramId mainProgramId = _config.ProgramRegistryService.GetProgramIdByIndex(programId, programIndex: 0);

        if (mainProgramId != ProgramId.InvalidId)
        {
            return mainProgramId;
        }

        // Get the current program's map info and return the main program ID.
        Optional<ProgramIndexMapInfo> mapInfo = _config.ProgramRegistryService.GetProgramIndexMapInfo(programId);

        if (mapInfo.HasValue)
        {
            return mapInfo.Value.MainProgramId;
        }

        // The program ID isn't in the program index map. Probably running a single-program application
        return programId;
    }

    public SaveDataTransferCryptoConfiguration GetSaveDataTransferCryptoConfiguration()
    {
        throw new NotImplementedException();
    }

    public Result GetSaveDataIndexCount(out int count)
    {
        UnsafeHelpers.SkipParamInit(out count);

        using var accessor = new UniqueRef<SaveDataIndexerAccessor>();
        Result res = OpenSaveDataIndexerAccessor(ref accessor.Ref, out bool _, SaveDataSpaceId.User);
        if (res.IsFailure()) return res.Miss();

        count = accessor.Get.GetInterface().GetIndexCount();
        return Result.Success;
    }

    public Result OpenSaveDataIndexerAccessor(ref UniqueRef<SaveDataIndexerAccessor> outAccessor,
        out bool isInitialOpen, SaveDataSpaceId spaceId)
    {
        return _config.SaveIndexerManager.OpenSaveDataIndexerAccessor(ref outAccessor, out isInitialOpen, spaceId).Ret();
    }

    public void ResetTemporaryStorageIndexer()
    {
        _config.SaveIndexerManager.ResetIndexer(SaveDataSpaceId.Temporary);
    }
}