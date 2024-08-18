using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;
using LibHac.FsSystem;
using LibHac.FsSystem.Save;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Util;
using static LibHac.FsSrv.Anonymous;
using Utility = LibHac.FsSrv.Impl.Utility;

namespace LibHac.FsSrv;

file static class Anonymous
{
    public static bool IsDeviceUniqueMac(SaveDataSpaceId spaceId)
    {
        return spaceId == SaveDataSpaceId.System ||
               spaceId == SaveDataSpaceId.User ||
               spaceId == SaveDataSpaceId.Temporary ||
               spaceId == SaveDataSpaceId.ProperSystem ||
               spaceId == SaveDataSpaceId.SafeMode;
    }

    public static Result WipeData(IFileSystem fileSystem, ref readonly Path filePath, RandomDataGenerator generateKey)
    {
        const int workBufferSize = 0x400000;
        const int minimumWorkBufferSize = 0x1000;

        Result lastResult = Result.Success;

        using (var file = new UniqueRef<IFile>())
        {
            Result res = fileSystem.OpenFile(ref file.Ref, in filePath, OpenMode.ReadWrite);
            if (res.IsFailure()) return res.Miss();

            try
            {
                res = file.Get.GetSize(out long remainingSize);
                if (res.IsFailure()) return res.Miss();

                Span<byte> key = stackalloc byte[Aes.KeySize128];
                Span<byte> counter = stackalloc byte[Aes.BlockSize];
                generateKey(key);

                using var workBuffer = new PooledBuffer();
                workBuffer.AllocateParticularlyLarge(workBufferSize, minimumWorkBufferSize);
                Span<byte> buffer = workBuffer.GetBuffer();

                buffer.Clear();
                counter.Clear();
                long offset = 0;

                while (remainingSize > 0)
                {
                    int writeSize = (int)Math.Min(remainingSize, buffer.Length);
                    int encryptSize = Alignment.AlignUp(writeSize, Aes.BlockSize);
                    Aes.EncryptCtr128(buffer.Slice(0, encryptSize), buffer.Slice(0, writeSize), key, counter);

                    Result result = file.Get.Write(offset, buffer.Slice(0, writeSize), WriteOption.None);
                    if (lastResult.IsSuccess() && result.IsFailure())
                        lastResult = result;

                    FsSystem.Utility.AddCounter(counter, (ulong)BitUtil.DivideUp(writeSize, Aes.BlockSize));

                    offset += writeSize;
                    remainingSize -= writeSize;
                }
            }
            finally
            {
                file.Get.Flush().IgnoreResult();
            }
        }

        if (lastResult.IsSuccess()) return lastResult.Miss();

        return Result.Success;
    }
}

/// <summary>
/// Handles the lower-level operations on save data.
/// <see cref="SaveDataFileSystemService"/> uses this class to provide save data APIs at a higher level of abstraction.
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class SaveDataFileSystemServiceImpl : IDisposable
{
    private static readonly bool UseTargetManager = true;

    private Configuration _config;
    private EncryptionSeed _encryptionSeed;

    private SaveDataFileSystemCacheManager _saveFileSystemCacheManager;
    private SaveDataExtraDataAccessorCacheManager _saveExtraDataCacheManager;
    private SaveDataPorterManager _saveDataPorterManager;
    private bool _isSdCardAccessible;
    private Optional<uint> _integritySaveDataVersion;
    private Optional<uint> _journalIntegritySaveDataVersion;
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

        public uint JournalIntegritySaveDataVersion;
        public uint JournalIntegritySupportedVersionMin;
        public uint JournalIntegritySupportedVersionMax;
        public uint IntegritySaveDataVersion;
        public uint IntegritySupportedVersionMin;
        public uint IntegritySupportedVersionMax;

        // LibHac additions
        public FileSystemServer FsServer;
    }

    public SaveDataFileSystemServiceImpl(in Configuration configuration)
    {
        _config = configuration;
        _saveFileSystemCacheManager = new SaveDataFileSystemCacheManager();
        _saveExtraDataCacheManager = new SaveDataExtraDataAccessorCacheManager();
        _saveDataPorterManager = new SaveDataPorterManager();

        _isSdCardAccessible = false;
        _integritySaveDataVersion = new Optional<uint>();
        _journalIntegritySaveDataVersion = new Optional<uint>();

        _timeStampGetter = new TimeStampGetter(this);

        Result res = _saveFileSystemCacheManager.Initialize(_config.SaveDataFileSystemCacheCount);
        Abort.DoAbortUnless(res.IsSuccess());

        IntegritySaveDataFileSystem.SetVersionSupported(_config.FsServer, _config.IntegritySupportedVersionMin, _config.IntegritySupportedVersionMax);
        JournalIntegritySaveDataFileSystem.SetVersionSupported(_config.FsServer, _config.JournalIntegritySupportedVersionMin, _config.JournalIntegritySupportedVersionMax);
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

    public void SetIntegritySaveDataVersion(uint version)
    {
        _integritySaveDataVersion.Set(version);
    }

    public void SetJournalIntegritySaveDataVersion(uint version)
    {
        _journalIntegritySaveDataVersion.Set(version);
    }

    public uint GetIntegritySaveDataVersion()
    {
        if (_integritySaveDataVersion.HasValue)
            return _integritySaveDataVersion.Value;

        return _config.IntegritySaveDataVersion;
    }

    public uint GetJournalIntegritySaveDataVersion()
    {
        if (_journalIntegritySaveDataVersion.HasValue)
            return _journalIntegritySaveDataVersion.Value;

        return _config.JournalIntegritySaveDataVersion;
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

    private Result GetSaveDataCommitTimeStamp(out long timeStamp)
    {
        return _config.TimeService.GetCurrentPosixTime(out timeStamp).Ret();
    }

    public Result OpenSaveDataFile(ref SharedRef<IFile> outFile, SaveDataSpaceId spaceId, ulong saveDataId,
        OpenMode openMode)
    {
        using var fileSystem = new SharedRef<IFileSystem>();
        Result res = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId);
        if (res.IsFailure()) return res.Miss();

        using var saveDataFile = new SharedRef<IFile>();

        _saveFileSystemCacheManager.Unregister(spaceId, saveDataId);

        res = _config.SaveFsCreator.CreateRaw(ref saveDataFile.Ref, in fileSystem, saveDataId, openMode);
        if (res.IsFailure()) return res.Miss();

        outFile.SetByMove(ref saveDataFile.Ref);
        return Result.Success;
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

                res = _config.SaveFsCreator.Create(ref saveDataFs.Ref, in fileSystem, spaceId, saveDataId,
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
            new SaveDataFileSystemCacheRegister(in saveDataFs, _saveFileSystemCacheManager, spaceId, saveDataId));

        if (openReadOnly)
        {
            using SharedRef<IFileSystem> tempFs = SharedRef<IFileSystem>.CreateMove(ref registerFs.Ref);
            using var readOnlyFileSystem = new SharedRef<ReadOnlyFileSystem>(new ReadOnlyFileSystem(in tempFs));

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

        return OpenSaveDataDirectoryFileSystemImpl(ref outFileSystem, spaceId, saveDataId, in saveDataMetaIdDirectoryName).Ret();
    }

    public Result OpenSaveDataInternalStorageFileSystem(ref SharedRef<IFileSystem> outFileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId, ref readonly Path saveDataRootPath, bool useSecondMacKey,
        bool isReconstructible)
    {
        using var fileSystem = new SharedRef<IFileSystem>();
        Result res = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId, in saveDataRootPath, allowEmulatedSave: true);
        if (res.IsFailure()) return res.Miss();

        using UniqueLockRef<SdkRecursiveMutexType> scopedLockFsCache = _saveFileSystemCacheManager.GetScopedLock();
        using UniqueLockRef<SdkRecursiveMutexType> scopedLockExtraDataCache = _saveExtraDataCacheManager.GetScopedLock();

        _saveFileSystemCacheManager.Unregister(spaceId, saveDataId);

        using var internalStorage = new SharedRef<IFileSystem>();
        res = _config.SaveFsCreator.CreateInternalStorage(ref internalStorage.Ref, in fileSystem, spaceId, saveDataId,
            IsDeviceUniqueMac(spaceId), useSecondMacKey, _timeStampGetter, isReconstructible);
        if (res.IsFailure()) return res.Miss();

        outFileSystem.SetByMove(ref internalStorage.Ref);
        return Result.Success;
    }

    private Result OpenSaveDataImageFile(ref UniqueRef<IFile> outFile, SaveDataSpaceId spaceId, ulong saveDataId,
        ref readonly Path saveDataRootPath)
    {
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        using var fileSystem = new SharedRef<IFileSystem>();
        Result res = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId, in saveDataRootPath, allowEmulatedSave: true);
        if (res.IsFailure()) return res.Miss();

        using scoped var saveImageName = new Path();
        res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.Get.GetEntryType(out DirectoryEntryType type, in saveImageName);

        if (res.IsFailure())
        {
            if (ResultFs.PathNotFound.Includes(res))
                return ResultFs.TargetNotFound.LogConverted(res);

            return res.Miss();
        }

        if (type == DirectoryEntryType.File)
        {
            res = fileSystem.Get.OpenFile(ref outFile.Ref, in saveImageName, OpenMode.ReadWrite);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        return ResultFs.IncompatiblePath.Log();
    }

    private Result OpenSaveDataExtensionContextFile(ref UniqueRef<IFile> outFile, IFile saveDataFile, ulong saveDataId,
        SaveDataSpaceId spaceId, long dataSize, long journalSize, bool createIfMissing, bool isReconstructible)
    {
        Result result = OpenSaveDataMeta(ref outFile.Ref, saveDataId, spaceId, SaveDataMetaType.ExtensionContext);

        if (result.IsFailure())
        {
            if (ResultFs.PathNotFound.Includes(result))
            {
                // Create the extension context file if it's missing and we were asked to create it
                if (!createIfMissing)
                    return result.Rethrow();

                using var saveDataStorage = new SharedRef<IStorage>(new FileStorage(saveDataFile));

                // Check that the new save data sizes aren't smaller than the current sizes
                using var extraDataAccessor = new SharedRef<ISaveDataExtraDataAccessor>();
                Result res = _config.SaveFsCreator.CreateExtraDataAccessor(ref extraDataAccessor.Ref,
                    in saveDataStorage, IsDeviceUniqueMac(spaceId), isIntegritySaveData: true, isReconstructible);
                if (res.IsFailure()) return res.Miss();

                res = extraDataAccessor.Get.ReadExtraData(out SaveDataExtraData extraData);
                if (res.IsFailure()) return res.Miss();

                if (dataSize < extraData.DataSize || journalSize < extraData.JournalSize)
                    return ResultFs.InvalidSize.Log();

                // Calculate the size needed for the context file, and create it
                var extender = new SaveDataExtender();

                res = _config.SaveFsCreator.ExtractSaveDataParameters(out JournalIntegritySaveDataParameters parameters,
                    saveDataStorage.Get, IsDeviceUniqueMac(spaceId), isReconstructible);
                if (res.IsFailure()) return res.Miss();

                res = extender.InitializeContext(in parameters, dataSize, journalSize);
                if (res.IsFailure()) return res.Miss();

                long contextFileSize = SaveDataExtender.QueryContextSize() + extender.GetLogSize();
                res = CreateSaveDataMeta(saveDataId, spaceId, SaveDataMetaType.ExtensionContext, contextFileSize);
                if (res.IsFailure()) return res.Miss();

                // Once the context file is created, write the initial extension context to it
                res = OpenSaveDataMeta(ref outFile.Ref, saveDataId, spaceId, SaveDataMetaType.ExtensionContext);
                if (res.IsFailure()) return res.Miss();

                using var contextStorage = new FileStorage(outFile.Get);
                extender.WriteContext(contextStorage).IgnoreResult();
            }
            else
            {
                return result.Miss();
            }
        }

        return Result.Success;
    }

    public Result StartExtendSaveDataFileSystem(out long extendedTotalSize, ulong saveDataId, SaveDataSpaceId spaceId,
        SaveDataType type, long dataSize, long journalSize, ref readonly Path saveDataRootPath)
    {
        return ExtendSaveDataFileSystemCore(out extendedTotalSize, saveDataId, spaceId, type, dataSize, journalSize,
            in saveDataRootPath, isExtensionStart: true).Ret();
    }

    public Result ResumeExtendSaveDataFileSystem(out long extendedTotalSize, ulong saveDataId, SaveDataSpaceId spaceId,
        SaveDataType type, ref readonly Path saveDataRootPath)
    {
        return ExtendSaveDataFileSystemCore(out extendedTotalSize, saveDataId, spaceId, type, dataSize: 0,
            journalSize: 0, in saveDataRootPath, isExtensionStart: false).Ret();
    }

    private Result ExtendSaveDataFileSystemCore(out long extendedTotalSize, ulong saveDataId, SaveDataSpaceId spaceId,
        SaveDataType type, long dataSize, long journalSize, ref readonly Path saveDataRootPath, bool isExtensionStart)
    {
        UnsafeHelpers.SkipParamInit(out extendedTotalSize);

        _saveFileSystemCacheManager.Unregister(spaceId, saveDataId);

        using var saveDataFile = new UniqueRef<IFile>();
        using var contextFile = new UniqueRef<IFile>();

        Result res;
        Result result = OpenSaveDataImageFile(ref saveDataFile.Ref, spaceId, saveDataId, in saveDataRootPath);

        if (result.IsFailure())
        {
            // OpenSaveDataImageFile returns ResultIncompatiblePath when the target is a directory
            if (ResultFs.IncompatiblePath.Includes(result))
            {
                // All we need to do for directory save data is update the sizes in its extra data
                res = ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId, saveDataId, type, in saveDataRootPath);
                if (res.IsFailure()) return res.Miss();

                extraData.DataSize = dataSize;
                extraData.JournalSize = journalSize;

                res = WriteSaveDataFileSystemExtraData(spaceId, saveDataId, in extraData, in saveDataRootPath, type, updateTimeStamp: true);
                if (res.IsFailure()) return res.Miss();

                return Result.Success;
            }

            return result.Miss();
        }

        res = OpenSaveDataExtensionContextFile(ref contextFile.Ref, saveDataFile.Get, saveDataId, spaceId, dataSize,
            journalSize, isExtensionStart, SaveDataProperties.IsReconstructible(type, spaceId));
        if (res.IsFailure()) return res.Miss();

        using var saveDataStorage = new SharedRef<IStorage>(new FileStorage(saveDataFile.Get));
        var contextStorage = new FileStorage(contextFile.Get);

        var extender = new SaveDataExtender();
        res = extender.ReadContext(contextStorage);
        if (res.IsFailure()) return res.Miss();

        if (isExtensionStart)
        {
            if (extender.GetAvailableSize() != dataSize)
                return ResultFs.DifferentSaveDataExtensionContextParameter.Log();

            if (extender.GetJournalSize() != journalSize)
                return ResultFs.DifferentSaveDataExtensionContextParameter.Log();
        }

        extendedTotalSize = extender.GetExtendedSaveDataSize();

        res = saveDataStorage.Get.GetSize(out long currentSize);
        if (res.IsFailure()) return res.Miss();

        if (currentSize < extendedTotalSize)
        {
            res = saveDataStorage.Get.SetSize(extendedTotalSize);
            if (res.IsFailure()) return res.Miss();
        }

        using var saveDataSubStorage = new ValueSubStorage(saveDataStorage.Get, 0, extendedTotalSize);
        using var logSubStorage = new ValueSubStorage(contextStorage, SaveDataExtender.QueryContextSize(), extender.GetLogSize());

        res = _config.SaveFsCreator.ExtendSaveData(extender, in saveDataSubStorage, in logSubStorage,
            IsDeviceUniqueMac(spaceId), SaveDataProperties.IsReconstructible(type, spaceId));
        if (res.IsFailure()) return res.Miss();

        using (var extraDataAccessor = new SharedRef<ISaveDataExtraDataAccessor>())
        {
            res = _config.SaveFsCreator.CreateExtraDataAccessor(ref extraDataAccessor.Ref, in saveDataStorage,
                IsDeviceUniqueMac(spaceId), isIntegritySaveData: true,
                isReconstructible: SaveDataProperties.IsReconstructible(type, spaceId));
            if (res.IsFailure()) return res.Miss();

            res = extraDataAccessor.Get.ReadExtraData(out SaveDataExtraData extraData);
            if (res.IsFailure()) return res.Miss();

            extraData.DataSize = extender.GetAvailableSize();
            extraData.JournalSize = extender.GetJournalSize();

            _config.GenerateRandomData(SpanHelpers.AsByteSpan(ref extraData.CommitId));

            if (GetSaveDataCommitTimeStamp(out long timeStamp).IsSuccess())
                extraData.TimeStamp = timeStamp;

            res = extraDataAccessor.Get.WriteExtraData(in extraData);
            if (res.IsFailure()) return res.Miss();

            res = extraDataAccessor.Get.CommitExtraData(updateTimeStamp: true);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }

    public Result FinishExtendSaveDataFileSystem(ulong saveDataId, SaveDataSpaceId spaceId)
    {
        Result res = DeleteSaveDataMeta(saveDataId, spaceId, SaveDataMetaType.ExtensionContext);

        if (res.IsFailure())
        {
            if (ResultFs.PathNotFound.Includes(res))
            {
                res.Catch().Handle();
            }
            else
            {
                return res.Miss();
            }
        }

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

        res = OpenSaveDataDirectoryFileSystemImpl(ref fileSystem.Ref, spaceId, saveDataId, in saveDataMetaDirectoryName, createIfMissing: false);
        if (res.IsFailure()) return res.Miss();

        using scoped var saveDataIdDirectoryName = new Path();
        PathFunctions.SetUpFixedPathSaveId(ref saveDataIdDirectoryName.Ref(), saveDataIdDirectoryNameBuffer, saveDataId);
        if (res.IsFailure()) return res.Miss();

        // Delete the save data's meta directory, ignoring the error if the directory is already gone
        res = fileSystem.Get.DeleteDirectoryRecursively(in saveDataIdDirectoryName);

        if (res.IsFailure())
        {
            if (ResultFs.PathNotFound.Includes(res))
            {
                res.Catch().Handle();
            }

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

    public Result QuerySaveDataTotalSize(out long totalSize, long blockSize, long dataSize, long journalSize)
    {
        JournalIntegritySaveDataParameters saveParams =
            JournalIntegritySaveDataFileSystemDriver.SetUpSaveDataParameters(blockSize, dataSize, journalSize);

        Result res = JournalIntegritySaveDataFileSystemDriver.QueryTotalSize(out totalSize, saveParams.BlockSize,
            saveParams.CountDataBlock, saveParams.CountJournalBlock, saveParams.CountExpandMax,
            GetJournalIntegritySaveDataVersion());
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
        Result res = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId, in saveDataRootPath, allowEmulatedSave: false);
        if (res.IsFailure()) return res.Miss();

        using scoped var saveImageName = new Path();
        res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
        if (res.IsFailure()) return res.Miss();

        // Check if we need to create a pseudo save data, i.e. directory save data
        bool isPseudoSaveFs = _config.IsPseudoSaveData();
        bool isJournalingSupported = SaveDataProperties.IsJournalingSupported(creationInfo.FormatType);
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
                long totalSize;
                const long blockSize = 0x4000;

                var integrityParams = new Optional<IntegritySaveDataParameters>();
                var journalIntegrityParams = new Optional<JournalIntegritySaveDataParameters>();

                if (!isJournalingSupported)
                {
                    IntegritySaveDataParameters param = IntegritySaveDataFileSystemDriver.SetUpSaveDataParameters(blockSize, dataSize);

                    res = IntegritySaveDataFileSystemDriver.QueryTotalSize(out totalSize, param.BlockSize,
                        param.BlockCount, GetIntegritySaveDataVersion());
                    if (res.IsFailure()) return res.Miss();

                    integrityParams.Set(in param);
                }
                else
                {
                    JournalIntegritySaveDataParameters param =
                        JournalIntegritySaveDataFileSystemDriver.SetUpSaveDataParameters(blockSize, dataSize, journalSize);

                    res = JournalIntegritySaveDataFileSystemDriver.QueryTotalSize(out totalSize, param.BlockSize,
                        param.CountDataBlock, param.CountJournalBlock, param.CountExpandMax,
                        GetJournalIntegritySaveDataVersion());
                    if (res.IsFailure()) return res.Miss();

                    journalIntegrityParams.Set(in param);
                }

                res = fileSystem.Get.CreateFile(in saveImageName, totalSize);
                if (res.IsFailure()) return res.Miss();

                if (skipFormat)
                    return Result.Success;

                using var fileStorage = new SharedRef<FileStorageBasedFileSystem>(new FileStorageBasedFileSystem());
                if (!fileStorage.HasValue)
                    return ResultFs.AllocationMemoryFailedInSaveDataFileSystemServiceImplA.Log();

                res = fileStorage.Get.Initialize(ref fileSystem.Ref, in saveImageName, OpenMode.ReadWrite);
                if (res.IsFailure()) return res.Miss();

                if (!isJournalingSupported)
                {
                    Assert.SdkAssert(integrityParams.HasValue);
                    IntegritySaveDataParameters param = integrityParams.ValueRo;

                    using var subStorage = new ValueSubStorage(fileStorage.Get, 0, totalSize);

                    res = _config.SaveFsCreator.FormatAsIntegritySaveData(in subStorage, param.BlockSize,
                        param.BlockCount, _config.BufferManager, IsDeviceUniqueMac(spaceId), _config.GenerateRandomData,
                        SaveDataProperties.IsReconstructible(creationInfo.Attribute.Type, spaceId),
                        GetIntegritySaveDataVersion());
                    if (res.IsFailure()) return res.Miss();
                }
                else
                {
                    var hashSaltRandom = new Optional<HashSalt>();
                    if (!creationInfo.IsHashSaltEnabled)
                    {
                        hashSaltRandom.Set();
                        _config.GenerateRandomData(SpanHelpers.AsByteSpan(ref hashSaltRandom.Value));
                    }

                    Assert.SdkAssert(journalIntegrityParams.HasValue);
                    JournalIntegritySaveDataParameters param = journalIntegrityParams.ValueRo;

                    using var subStorage = new ValueSubStorage(fileStorage.Get, 0, totalSize);

                    ref readonly HashSalt hashSalt = ref creationInfo.IsHashSaltEnabled ? ref creationInfo.HashSalt : ref hashSaltRandom.ValueRo;

                    res = _config.SaveFsCreator.Format(in subStorage, param.BlockSize, param.CountExpandMax,
                        param.CountDataBlock, param.CountJournalBlock, _config.BufferManager,
                        IsDeviceUniqueMac(spaceId), in hashSalt, _config.GenerateRandomData,
                        SaveDataProperties.IsReconstructible(creationInfo.Attribute.Type, spaceId),
                        GetJournalIntegritySaveDataVersion());
                    if (res.IsFailure()) return res.Miss();
                }
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

        _saveFileSystemCacheManager.Unregister(spaceId, saveDataId);

        // Open the directory containing the save data
        using var fileSystem = new SharedRef<IFileSystem>();
        Result res = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId, in saveDataRootPath, allowEmulatedSave: false);
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
                WipeData(fileSystem.Get, in saveImageName, _config.GenerateRandomData).IgnoreResult();
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
        in SaveDataExtraData extraData, ref readonly Path saveDataRootPath, SaveDataType type, bool updateTimeStamp)
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
        Unsafe.SkipInit(out Array18<byte> saveImageNameBuffer);

        _saveFileSystemCacheManager.Unregister(spaceId, saveDataId);

        using var fileSystem = new SharedRef<IFileSystem>();

        // Open the directory containing the save data
        Result res = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId, in saveDataRootPath, allowEmulatedSave: false);
        if (res.IsFailure()) return res.Miss();

        using scoped var saveImageName = new Path();
        res = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
        if (res.IsFailure()) return res.Miss();

        // Check if the save data is a file or a directory
        res = fileSystem.Get.GetEntryType(out DirectoryEntryType entryType, in saveImageName);
        if (res.IsFailure()) return res.Miss();

        if (entryType != DirectoryEntryType.File)
            return ResultFs.PreconditionViolation.Log();

        using var file = new UniqueRef<IFile>();
        fileSystem.Get.OpenFile(ref file.Ref, in saveImageName, OpenMode.Write);
        if (res.IsFailure()) return res.Miss();

        const int bufferSize = 0x200;
        const int corruptWriteSize = 0x4000;

        Span<byte> buffer = stackalloc byte[bufferSize];
        buffer.Fill(0xAA);

        for (int i = 0; i < corruptWriteSize; i += bufferSize)
        {
            res = file.Get.Write(offset + i, buffer, WriteOption.None);
            if (res.IsFailure()) return res.Miss();
        }

        res = file.Get.Flush();
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result RecoverSaveDataFileSystemMasterHeader(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        using var fileSystem = new SharedRef<IFileSystem>();
        Result res = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId);
        if (res.IsFailure()) return res.Miss();

        _saveFileSystemCacheManager.Unregister(spaceId, saveDataId);

        res = _config.SaveFsCreator.RecoverMasterHeader(in fileSystem, saveDataId, _config.BufferManager,
            IsDeviceUniqueMac(spaceId), isReconstructible: false);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result UpdateSaveDataFileSystemMac(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        using var fileSystem = new SharedRef<IFileSystem>();
        Result res = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, spaceId, saveDataId);
        if (res.IsFailure()) return res.Miss();

        _saveFileSystemCacheManager.Unregister(spaceId, saveDataId);

        res = _config.SaveFsCreator.UpdateMac(in fileSystem, saveDataId, IsDeviceUniqueMac(spaceId), isReconstructible: false);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private bool IsSaveEmulated(ref readonly Path saveDataRootPath)
    {
        return !saveDataRootPath.IsEmpty();
    }

    public Result OpenSaveDataDirectoryFileSystem(ref SharedRef<IFileSystem> outFileSystem, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        using var rootPath = new Path();

        return OpenSaveDataDirectoryFileSystem(ref outFileSystem, spaceId, saveDataId, in rootPath, allowEmulatedSave: true).Ret();
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
        ReadOnlySpan<byte> saveDirName = spaceId == SaveDataSpaceId.Temporary ? "/temp"u8 : "/save"u8;

        res = PathFunctions.SetUpFixedPath(ref saveDataAreaDirectoryName.Ref(), saveDirName);
        if (res.IsFailure()) return res.Miss();

        res = OpenSaveDataDirectoryFileSystemImpl(ref outFileSystem, spaceId, saveDataId, in saveDataAreaDirectoryName);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private Result OpenSaveDataDirectoryFileSystemImpl(ref SharedRef<IFileSystem> outFileSystem, SaveDataSpaceId spaceId,
        ulong saveDataId, ref readonly Path directoryPath, bool createIfMissing)
    {
        using var baseFileSystem = new SharedRef<IFileSystem>();

        switch (spaceId)
        {
            case SaveDataSpaceId.System:
            {
                Result res = _config.BaseFsService.OpenBisFileSystem(ref baseFileSystem.Ref, BisPartitionId.System,
                    caseSensitive: true);
                if (res.IsFailure()) return res.Miss();

                res = Utility.WrapSubDirectory(ref outFileSystem, in baseFileSystem, in directoryPath,
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

                res = Utility.WrapSubDirectory(ref outFileSystem, in baseFileSystem, in directoryPath, createIfMissing);
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

                res = Utility.WrapSubDirectory(ref baseFileSystem.Ref, in tempFileSystem, in pathSdRoot, createIfMissing);
                if (res.IsFailure()) return res.Miss();

                res = _config.EncryptedFsCreator.Create(ref outFileSystem, in baseFileSystem,
                    IEncryptedFileSystemCreator.KeyId.Save, in _encryptionSeed);
                if (res.IsFailure()) return res.Miss();

                break;
            }

            case SaveDataSpaceId.ProperSystem:
            {
                Result res = _config.BaseFsService.OpenBisFileSystem(ref baseFileSystem.Ref,
                    BisPartitionId.SystemProperPartition, caseSensitive: true);
                if (res.IsFailure()) return res.Miss();

                res = Utility.WrapSubDirectory(ref outFileSystem, in baseFileSystem, in directoryPath, createIfMissing);
                if (res.IsFailure()) return res.Miss();

                break;
            }

            case SaveDataSpaceId.SafeMode:
            {
                Result res = _config.BaseFsService.OpenBisFileSystem(ref baseFileSystem.Ref, BisPartitionId.SafeMode,
                    caseSensitive: true);
                if (res.IsFailure()) return res.Miss();

                res = Utility.WrapSubDirectory(ref outFileSystem, in baseFileSystem, in directoryPath, createIfMissing);
                if (res.IsFailure()) return res.Miss();

                break;
            }

            default:
                return ResultFs.InvalidArgument.Log();
        }

        return Result.Success;
    }

    private Result OpenSaveDataDirectoryFileSystemImpl(ref SharedRef<IFileSystem> outFileSystem, SaveDataSpaceId spaceId,
        ulong saveDataId, ref readonly Path directoryPath)
    {
        return OpenSaveDataDirectoryFileSystemImpl(ref outFileSystem, spaceId, saveDataId, in directoryPath, createIfMissing: true).Ret();
    }

    public Result IsProvisionallyCommittedSaveData(out bool isProvisionallyCommitted, in SaveDataInfo saveInfo)
    {
        UnsafeHelpers.SkipParamInit(out isProvisionallyCommitted);

        using var fileSystem = new SharedRef<IFileSystem>();
        Result res = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref, saveInfo.SpaceId, saveInfo.SaveDataId);
        if (res.IsFailure()) return res.Miss();

        res = _config.SaveFsCreator.IsProvisionallyCommittedSaveData(out isProvisionallyCommitted, in fileSystem,
            in saveInfo, IsDeviceUniqueMac(saveInfo.SpaceId), _timeStampGetter,
            SaveDataProperties.IsReconstructible(saveInfo.Type, saveInfo.SpaceId));
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
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
        // Get the current program's map info and return the main program ID.
        Optional<ProgramIndexMapInfo> programIndexMapInfo = _config.ProgramRegistryService.GetProgramIndexMapInfo(programId);

        if (programIndexMapInfo.HasValue)
        {
            return programIndexMapInfo.Value.MainProgramId;
        }

        // The program ID isn't in the program index map. Probably running a single-program application
        return programId;
    }

    public SaveDataTransferCryptoConfiguration GetSaveDataTransferCryptoConfiguration()
    {
        return _config.SaveTransferCryptoConfig;
    }

    public SaveDataPorterManager GetSaveDataPorterManager()
    {
        return _saveDataPorterManager;
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