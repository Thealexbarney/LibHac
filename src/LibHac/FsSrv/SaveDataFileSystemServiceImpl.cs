using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Util;
using Utility = LibHac.FsSrv.Impl.Utility;

namespace LibHac.FsSrv
{
    public class SaveDataFileSystemServiceImpl
    {
        private Configuration _config;
        private EncryptionSeed _encryptionSeed;

        private SaveDataFileSystemCacheManager _saveDataFsCacheManager;
        private SaveDataExtraDataAccessorCacheManager _extraDataCacheManager;
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
                return _saveService.GetSaveDataCommitTimeStamp(out timeStamp);
            }
        }

        public SaveDataFileSystemServiceImpl(in Configuration configuration)
        {
            _config = configuration;
            _saveDataFsCacheManager = new SaveDataFileSystemCacheManager();
            _extraDataCacheManager = new SaveDataExtraDataAccessorCacheManager();

            _timeStampGetter = new TimeStampGetter(this);

            Result rc = _saveDataFsCacheManager.Initialize(_config.MaxSaveFsCacheCount);
            Abort.DoAbortUnless(rc.IsSuccess());
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
            public int MaxSaveFsCacheCount;
            public Func<bool> IsPseudoSaveData;
            public ISaveDataIndexerManager SaveIndexerManager;

            // LibHac additions
            public FileSystemServer FsServer;
        }

        internal Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            var registry = new ProgramRegistryImpl(_config.FsServer);
            return registry.GetProgramInfo(out programInfo, processId);
        }

        public Result DoesSaveDataEntityExist(out bool exists, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out exists);

            using var fileSystem = new SharedRef<IFileSystem>();

            Result rc = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref(), spaceId);
            if (rc.IsFailure()) return rc.Miss();

            // Get the path of the save data
            // Hack around error CS8350.
            const int bufferLength = 0x12;
            Span<byte> buffer = stackalloc byte[bufferLength];
            ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
            Span<byte> saveImageNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

            using var saveImageName = new Path();
            rc = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
            if (rc.IsFailure()) return rc.Miss();

            rc = fileSystem.Get.GetEntryType(out _, in saveImageName);

            if (rc.IsSuccess())
            {
                exists = true;
                return Result.Success;
            }
            else if (ResultFs.PathNotFound.Includes(rc))
            {
                exists = false;
                return Result.Success;
            }
            else
            {
                return rc.Miss();
            }
        }

        public Result OpenSaveDataFileSystem(ref SharedRef<IFileSystem> outFileSystem, SaveDataSpaceId spaceId,
            ulong saveDataId, in Path saveDataRootPath, bool openReadOnly, SaveDataType type, bool cacheExtraData)
        {
            using var fileSystem = new SharedRef<IFileSystem>();

            Result rc = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref(), spaceId, in saveDataRootPath, true);
            if (rc.IsFailure()) return rc.Miss();

            bool allowDirectorySaveData = IsAllowedDirectorySaveData2(spaceId, in saveDataRootPath);

            // Note: When directory save data is allowed, Nintendo creates the save directory if it doesn't exist.
            // This bypasses normal save data creation, leaving the save with empty extra data.
            // Instead, we return that the save doesn't exist if the directory is missing.

            using var saveDataFs = new SharedRef<IFileSystem>();
            using var cachedFs = new SharedRef<SaveDataFileSystemHolder>();

            // Note: Nintendo doesn't cache directory save data
            // if (!allowDirectorySaveData)
            {
                // Check if we have the requested file system cached
                if (_saveDataFsCacheManager.GetCache(ref cachedFs.Ref(), spaceId, saveDataId))
                {
                    using var registerBase = new SharedRef<IFileSystem>(
                        new SaveDataFileSystemCacheRegisterBase<SaveDataFileSystemHolder>(ref cachedFs.Ref(),
                            _saveDataFsCacheManager));

                    using var resultConvertFs = new SharedRef<SaveDataResultConvertFileSystem>(
                        new SaveDataResultConvertFileSystem(ref registerBase.Ref()));

                    saveDataFs.SetByMove(ref resultConvertFs.Ref());
                }
            }

            // Create a new file system if it's not in the cache
            if (!saveDataFs.HasValue)
            {
                using UniqueLockRef<SdkRecursiveMutexType> scopedLock = _extraDataCacheManager.GetScopedLock();
                using var extraDataAccessor = new SharedRef<ISaveDataExtraDataAccessor>();

                bool openShared = SaveDataProperties.IsSharedOpenNeeded(type);
                bool isMultiCommitSupported = SaveDataProperties.IsMultiCommitSupported(type);
                bool isJournalingSupported = SaveDataProperties.IsJournalingSupported(type);
                bool useDeviceUniqueMac = IsDeviceUniqueMac(spaceId);

                rc = _config.SaveFsCreator.Create(ref saveDataFs.Ref(), ref extraDataAccessor.Ref(),
                    _saveDataFsCacheManager, ref fileSystem.Ref(), spaceId, saveDataId, allowDirectorySaveData,
                    useDeviceUniqueMac, isJournalingSupported, isMultiCommitSupported, openReadOnly, openShared,
                    _timeStampGetter);
                if (rc.IsFailure()) return rc.Miss();

                // Cache the extra data accessor if needed
                if (cacheExtraData && extraDataAccessor.HasValue)
                {
                    extraDataAccessor.Get.RegisterCacheObserver(_extraDataCacheManager, spaceId, saveDataId);

                    rc = _extraDataCacheManager.Register(in extraDataAccessor, spaceId, saveDataId);
                    if (rc.IsFailure()) return rc.Miss();
                }
            }

            if (openReadOnly)
            {
                outFileSystem.Reset(new ReadOnlyFileSystem(ref saveDataFs.Ref()));
            }
            else
            {
                outFileSystem.SetByMove(ref saveDataFs.Ref());
            }

            return Result.Success;
        }

        public Result OpenSaveDataMetaDirectoryFileSystem(ref SharedRef<IFileSystem> outFileSystem,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            // Hack around error CS8350.
            const int bufferLength = 0x1B;
            Span<byte> buffer = stackalloc byte[bufferLength];
            ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
            Span<byte> saveDataMetaIdDirectoryNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

            using var saveDataMetaIdDirectoryName = new Path();
            Result rc = PathFunctions.SetUpFixedPathSaveMetaDir(ref saveDataMetaIdDirectoryName.Ref(),
                saveDataMetaIdDirectoryNameBuffer, saveDataId);
            if (rc.IsFailure()) return rc.Miss();

            return OpenSaveDataDirectoryFileSystemImpl(ref outFileSystem, spaceId, in saveDataMetaIdDirectoryName);
        }

        public Result OpenSaveDataInternalStorageFileSystem(ref SharedRef<IFileSystem> outFileSystem,
            SaveDataSpaceId spaceId, ulong saveDataId, in Path saveDataRootPath, bool useSecondMacKey)
        {
            throw new NotImplementedException();
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

            Result rc = OpenSaveDataMetaDirectoryFileSystem(ref fileSystem.Ref(), spaceId, saveDataId);
            if (rc.IsFailure()) return rc.Miss();

            // Hack around error CS8350.
            const int bufferLength = 0xF;
            Span<byte> buffer = stackalloc byte[bufferLength];
            ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
            Span<byte> saveDataMetaNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

            using var saveDataMetaName = new Path();
            rc = PathFunctions.SetUpFixedPathSaveMetaName(ref saveDataMetaName.Ref(), saveDataMetaNameBuffer,
                (uint)metaType);
            if (rc.IsFailure()) return rc.Miss();

            rc = fileSystem.Get.CreateFile(in saveDataMetaName, metaFileSize);
            if (rc.IsFailure()) return rc.Miss();

            return Result.Success;
        }

        public Result DeleteSaveDataMeta(ulong saveDataId, SaveDataSpaceId spaceId, SaveDataMetaType metaType)
        {
            using var fileSystem = new SharedRef<IFileSystem>();

            Result rc = OpenSaveDataMetaDirectoryFileSystem(ref fileSystem.Ref(), spaceId, saveDataId);
            if (rc.IsFailure()) return rc.Miss();

            // Hack around error CS8350.
            const int bufferLength = 0xF;
            Span<byte> buffer = stackalloc byte[bufferLength];
            ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
            Span<byte> saveDataMetaNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

            using var saveDataMetaName = new Path();
            rc = PathFunctions.SetUpFixedPathSaveMetaName(ref saveDataMetaName.Ref(), saveDataMetaNameBuffer,
                (uint)metaType);
            if (rc.IsFailure()) return rc.Miss();

            rc = fileSystem.Get.DeleteFile(in saveDataMetaName);
            if (rc.IsFailure()) return rc.Miss();

            return Result.Success;
        }

        public Result DeleteAllSaveDataMetas(ulong saveDataId, SaveDataSpaceId spaceId)
        {
            ReadOnlySpan<byte> metaDirName = // /saveMeta
                new[]
                {
                    (byte)'/', (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'M', (byte)'e', (byte)'t',
                    (byte)'a'
                };

            // Hack around error CS8350.
            const int bufferLength = 0x12;
            Span<byte> buffer = stackalloc byte[bufferLength];
            ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
            Span<byte> saveDataIdDirectoryNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

            using var fileSystem = new SharedRef<IFileSystem>();

            using var saveDataMetaDirectoryName = new Path();
            Result rc = PathFunctions.SetUpFixedPath(ref saveDataMetaDirectoryName.Ref(), metaDirName);
            if (rc.IsFailure()) return rc.Miss();

            rc = OpenSaveDataDirectoryFileSystemImpl(ref fileSystem.Ref(), spaceId, in saveDataMetaDirectoryName, false);
            if (rc.IsFailure()) return rc.Miss();

            using var saveDataIdDirectoryName = new Path();
            PathFunctions.SetUpFixedPathSaveId(ref saveDataIdDirectoryName.Ref(), saveDataIdDirectoryNameBuffer,
                saveDataId);
            if (rc.IsFailure()) return rc.Miss();

            // Delete the save data's meta directory, ignoring the error if the directory is already gone
            rc = fileSystem.Get.DeleteDirectoryRecursively(in saveDataIdDirectoryName);

            if (rc.IsFailure())
            {
                if (!ResultFs.PathNotFound.Includes(rc))
                    return rc.Catch().Handle();

                return rc.Miss();
            }

            return Result.Success;
        }

        public Result OpenSaveDataMeta(ref UniqueRef<IFile> outMetaFile, ulong saveDataId, SaveDataSpaceId spaceId,
            SaveDataMetaType metaType)
        {
            using var fileSystem = new SharedRef<IFileSystem>();

            Result rc = OpenSaveDataMetaDirectoryFileSystem(ref fileSystem.Ref(), spaceId, saveDataId);
            if (rc.IsFailure()) return rc.Miss();

            // Hack around error CS8350.
            const int bufferLength = 0xF;
            Span<byte> buffer = stackalloc byte[bufferLength];
            ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
            Span<byte> saveDataMetaNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

            using var saveDataMetaName = new Path();
            rc = PathFunctions.SetUpFixedPathSaveMetaName(ref saveDataMetaName.Ref(), saveDataMetaNameBuffer,
                (uint)metaType);
            if (rc.IsFailure()) return rc.Miss();

            rc = fileSystem.Get.OpenFile(ref outMetaFile, in saveDataMetaName, OpenMode.ReadWrite);
            if (rc.IsFailure()) return rc.Miss();

            return Result.Success;
        }

        public Result CreateSaveDataFileSystem(ulong saveDataId, in SaveDataAttribute attribute,
            in SaveDataCreationInfo creationInfo, in Path saveDataRootPath, in Optional<HashSalt> hashSalt,
            bool skipFormat)
        {
            // Use directory save data for now

            // Hack around error CS8350.
            const int bufferLength = 0x12;
            Span<byte> buffer = stackalloc byte[bufferLength];
            ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
            Span<byte> saveImageNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

            using var fileSystem = new SharedRef<IFileSystem>();

            Result rc = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref(), creationInfo.SpaceId,
                in saveDataRootPath, false);
            if (rc.IsFailure()) return rc.Miss();

            using var saveImageName = new Path();
            rc = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
            if (rc.IsFailure()) return rc.Miss();

            if (_config.IsPseudoSaveData())
            {
                rc = FsSystem.Utility.EnsureDirectory(fileSystem.Get, in saveImageName);
                if (rc.IsFailure()) return rc.Miss();

                using var saveFileSystem = new SharedRef<IFileSystem>();
                using var extraDataAccessor = new SharedRef<ISaveDataExtraDataAccessor>();

                bool isJournalingSupported = SaveDataProperties.IsJournalingSupported(attribute.Type);

                rc = _config.SaveFsCreator.Create(ref saveFileSystem.Ref(), ref extraDataAccessor.Ref(),
                    _saveDataFsCacheManager, ref fileSystem.Ref(), creationInfo.SpaceId, saveDataId,
                    allowDirectorySaveData: true, useDeviceUniqueMac: false, isJournalingSupported,
                    isMultiCommitSupported: false, openReadOnly: false, openShared: false, _timeStampGetter);
                if (rc.IsFailure()) return rc.Miss();

                var extraData = new SaveDataExtraData();
                extraData.Attribute = attribute;
                extraData.OwnerId = creationInfo.OwnerId;

                rc = GetSaveDataCommitTimeStamp(out extraData.TimeStamp);
                if (rc.IsFailure())
                    extraData.TimeStamp = 0;

                extraData.CommitId = 0;
                _config.GenerateRandomData(SpanHelpers.AsByteSpan(ref extraData.CommitId)).IgnoreResult();

                extraData.Flags = creationInfo.Flags;
                extraData.DataSize = creationInfo.Size;
                extraData.JournalSize = creationInfo.JournalSize;

                rc = extraDataAccessor.Get.WriteExtraData(in extraData);
                if (rc.IsFailure()) return rc.Miss();

                rc = extraDataAccessor.Get.CommitExtraData(true);
                if (rc.IsFailure()) return rc.Miss();
            }
            else
            {
                throw new NotImplementedException();
            }

            return Result.Success;
        }

        private Result WipeData(IFileSystem fileSystem, in Path filePath, RandomDataGenerator random)
        {
            throw new NotImplementedException();
        }

        public Result DeleteSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, bool wipeSaveFile,
            in Path saveDataRootPath)
        {
            // Hack around error CS8350.
            const int bufferLength = 0x12;
            Span<byte> buffer = stackalloc byte[bufferLength];
            ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
            Span<byte> saveImageNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

            using var fileSystem = new SharedRef<IFileSystem>();

            _saveDataFsCacheManager.Unregister(spaceId, saveDataId);

            // Open the directory containing the save data
            Result rc = OpenSaveDataDirectoryFileSystem(ref fileSystem.Ref(), spaceId, in saveDataRootPath, false);
            if (rc.IsFailure()) return rc.Miss();

            using var saveImageName = new Path();
            rc = PathFunctions.SetUpFixedPathSaveId(ref saveImageName.Ref(), saveImageNameBuffer, saveDataId);
            if (rc.IsFailure()) return rc.Miss();

            // Check if the save data is a file or a directory
            rc = fileSystem.Get.GetEntryType(out DirectoryEntryType entryType, in saveImageName);
            if (rc.IsFailure()) return rc.Miss();

            // Delete the save data, wiping the file if needed
            if (entryType == DirectoryEntryType.Directory)
            {
                rc = fileSystem.Get.DeleteDirectoryRecursively(in saveImageName);
                if (rc.IsFailure()) return rc.Miss();
            }
            else
            {
                if (wipeSaveFile)
                {
                    WipeData(fileSystem.Get, in saveImageName, _config.GenerateRandomData).IgnoreResult();
                }

                rc = fileSystem.Get.DeleteFile(in saveImageName);
                if (rc.IsFailure()) return rc.Miss();
            }

            return Result.Success;
        }

        public Result ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, SaveDataSpaceId spaceId,
            ulong saveDataId, SaveDataType type, in Path saveDataRootPath)
        {
            UnsafeHelpers.SkipParamInit(out extraData);

            // Nintendo returns blank extra data for directory save data.
            // We've extended directory save data to store extra data so we don't need to do that.

            using UniqueLockRef<SdkRecursiveMutexType> scopedLockFsCache = _saveDataFsCacheManager.GetScopedLock();
            using UniqueLockRef<SdkRecursiveMutexType> scopedLockExtraDataCache = _extraDataCacheManager.GetScopedLock();

            using var extraDataAccessor = new SharedRef<ISaveDataExtraDataAccessor>();

            // Try to grab an extra data accessor for the requested save from the cache.
            Result rc = _extraDataCacheManager.GetCache(ref extraDataAccessor.Ref(), spaceId, saveDataId);

            if (rc.IsSuccess())
            {
                // An extra data accessor was found in the cache. Read the extra data from it.
                return extraDataAccessor.Get.ReadExtraData(out extraData);
            }

            using var unusedSaveDataFs = new SharedRef<IFileSystem>();

            // We won't actually use the returned save data FS.
            // Opening the FS should cache an extra data accessor for it.
            rc = OpenSaveDataFileSystem(ref unusedSaveDataFs.Ref(), spaceId, saveDataId, saveDataRootPath,
                openReadOnly: true, type, cacheExtraData: true);
            if (rc.IsFailure()) return rc.Miss();

            // Try to grab an accessor from the cache again.
            rc = _extraDataCacheManager.GetCache(ref extraDataAccessor.Ref(), spaceId, saveDataId);

            if (rc.IsFailure())
            {
                // No extra data accessor was registered for the requested save data.
                // Return a blank extra data struct.
                extraData = new SaveDataExtraData();
                return rc;
            }

            rc = extraDataAccessor.Get.ReadExtraData(out extraData);
            if (rc.IsFailure()) return rc.Miss();

            return Result.Success;
        }

        public Result WriteSaveDataFileSystemExtraData(SaveDataSpaceId spaceId, ulong saveDataId,
            in SaveDataExtraData extraData, in Path saveDataRootPath, SaveDataType type, bool updateTimeStamp)
        {
            // Nintendo does nothing when writing directory save data extra data.
            // We've extended directory save data to store extra data so we don't return early.

            using UniqueLockRef<SdkRecursiveMutexType> scopedLockFsCache = _saveDataFsCacheManager.GetScopedLock();
            using UniqueLockRef<SdkRecursiveMutexType> scopedLockExtraDataCache = _extraDataCacheManager.GetScopedLock();

            using var extraDataAccessor = new SharedRef<ISaveDataExtraDataAccessor>();

            // Try to grab an extra data accessor for the requested save from the cache.
            Result rc = _extraDataCacheManager.GetCache(ref extraDataAccessor.Ref(), spaceId, saveDataId);

            if (rc.IsFailure())
            {
                // No accessor was found in the cache. Try to open one.
                using var unusedSaveDataFs = new SharedRef<IFileSystem>();

                // We won't actually use the returned save data FS.
                // Opening the FS should cache an extra data accessor for it.
                rc = OpenSaveDataFileSystem(ref unusedSaveDataFs.Ref(), spaceId, saveDataId, saveDataRootPath,
                    openReadOnly: false, type, cacheExtraData: true);
                if (rc.IsFailure()) return rc.Miss();

                // Try to grab an accessor from the cache again.
                rc = _extraDataCacheManager.GetCache(ref extraDataAccessor.Ref(), spaceId, saveDataId);

                if (rc.IsFailure())
                {
                    // No extra data accessor was registered for the requested save data, so don't do anything.
                    return Result.Success;
                }
            }

            // We should have a valid accessor if we've reached this point.
            // Write and commit the extra data.
            rc = extraDataAccessor.Get.WriteExtraData(in extraData);
            if (rc.IsFailure()) return rc.Miss();

            rc = extraDataAccessor.Get.CommitExtraData(updateTimeStamp);
            if (rc.IsFailure()) return rc.Miss();

            return Result.Success;
        }

        public Result CorruptSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long offset,
            in Path saveDataRootPath)
        {
            throw new NotImplementedException();
        }

        private Result GetSaveDataCommitTimeStamp(out long timeStamp)
        {
            return _config.TimeService.GetCurrentPosixTime(out timeStamp);
        }

        private bool IsSaveEmulated(in Path saveDataRootPath)
        {
            return !saveDataRootPath.IsEmpty();
        }

        public Result OpenSaveDataDirectoryFileSystem(ref SharedRef<IFileSystem> outFileSystem,
            SaveDataSpaceId spaceId)
        {
            using var rootPath = new Path();

            return OpenSaveDataDirectoryFileSystem(ref outFileSystem, spaceId, in rootPath, true);
        }

        public Result OpenSaveDataDirectoryFileSystem(ref SharedRef<IFileSystem> outFileSystem,
            SaveDataSpaceId spaceId, in Path saveDataRootPath, bool allowEmulatedSave)
        {
            Result rc;

            if (allowEmulatedSave && IsAllowedDirectorySaveData(spaceId, in saveDataRootPath))
            {
                using (var tmFileSystem = new SharedRef<IFileSystem>())
                {
                    // Ensure the target save data directory exists
                    rc = _config.TargetManagerFsCreator.Create(ref tmFileSystem.Ref(), in saveDataRootPath, false, true,
                        ResultFs.SaveDataRootPathUnavailable.Value);
                    if (rc.IsFailure()) return rc.Miss();
                }

                using var path = new Path();
                rc = path.Initialize(in saveDataRootPath);
                if (rc.IsFailure()) return rc.Miss();

                rc = _config.TargetManagerFsCreator.NormalizeCaseOfPath(out bool isTargetFsCaseSensitive, ref path.Ref());
                if (rc.IsFailure()) return rc.Miss();

                rc = _config.TargetManagerFsCreator.Create(ref outFileSystem, in path, isTargetFsCaseSensitive, false,
                    ResultFs.SaveDataRootPathUnavailable.Value);
                if (rc.IsFailure()) return rc.Miss();

                return Result.Success;
            }

            using var saveDataDirPath = new Path();
            ReadOnlySpan<byte> saveDirName;

            if (spaceId == SaveDataSpaceId.Temporary)
            {
                saveDirName = new[] { (byte)'/', (byte)'t', (byte)'e', (byte)'m', (byte)'p' }; // /temp
            }
            else
            {
                saveDirName = new[] { (byte)'/', (byte)'s', (byte)'a', (byte)'v', (byte)'e' }; // /save
            }

            rc = PathFunctions.SetUpFixedPath(ref saveDataDirPath.Ref(), saveDirName);
            if (rc.IsFailure()) return rc.Miss();

            rc = OpenSaveDataDirectoryFileSystemImpl(ref outFileSystem, spaceId, in saveDataDirPath, true);
            if (rc.IsFailure()) return rc.Miss();

            return Result.Success;
        }

        public Result OpenSaveDataDirectoryFileSystemImpl(ref SharedRef<IFileSystem> outFileSystem,
            SaveDataSpaceId spaceId, in Path basePath)
        {
            return OpenSaveDataDirectoryFileSystemImpl(ref outFileSystem, spaceId, in basePath, true);
        }

        public Result OpenSaveDataDirectoryFileSystemImpl(ref SharedRef<IFileSystem> outFileSystem,
            SaveDataSpaceId spaceId, in Path basePath, bool createIfMissing)
        {
            using var baseFileSystem = new SharedRef<IFileSystem>();

            Result rc;

            switch (spaceId)
            {
                case SaveDataSpaceId.System:
                    rc = _config.BaseFsService.OpenBisFileSystem(ref baseFileSystem.Ref(), BisPartitionId.System, true);
                    if (rc.IsFailure()) return rc.Miss();

                    rc = Utility.WrapSubDirectory(ref outFileSystem, ref baseFileSystem.Ref(), in basePath,
                        createIfMissing);
                    if (rc.IsFailure()) return rc.Miss();

                    return Result.Success;

                case SaveDataSpaceId.User:
                case SaveDataSpaceId.Temporary:
                    rc = _config.BaseFsService.OpenBisFileSystem(ref baseFileSystem.Ref(), BisPartitionId.User, true);
                    if (rc.IsFailure()) return rc.Miss();

                    rc = Utility.WrapSubDirectory(ref outFileSystem, ref baseFileSystem.Ref(), in basePath, createIfMissing);
                    if (rc.IsFailure()) return rc.Miss();

                    return Result.Success;

                case SaveDataSpaceId.SdSystem:
                case SaveDataSpaceId.SdCache:
                {
                    rc = _config.BaseFsService.OpenSdCardProxyFileSystem(ref baseFileSystem.Ref(), true);
                    if (rc.IsFailure()) return rc.Miss();

                    // Hack around error CS8350.
                    const int bufferLength = 0x40;
                    Span<byte> buffer = stackalloc byte[bufferLength];
                    ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
                    Span<byte> pathParentBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

                    using var pathParent = new Path();
                    rc = PathFunctions.SetUpFixedPathSingleEntry(ref pathParent.Ref(), pathParentBuffer,
                        CommonPaths.SdCardNintendoRootDirectoryName);
                    if (rc.IsFailure()) return rc.Miss();

                    using var pathSdRoot = new Path();
                    rc = pathSdRoot.Combine(in pathParent, in basePath);
                    if (rc.IsFailure()) return rc.Miss();

                    using SharedRef<IFileSystem> tempFileSystem =
                        SharedRef<IFileSystem>.CreateMove(ref baseFileSystem.Ref());
                    rc = Utility.WrapSubDirectory(ref baseFileSystem.Ref(), ref tempFileSystem.Ref(), in pathSdRoot, createIfMissing);
                    if (rc.IsFailure()) return rc.Miss();

                    rc = _config.EncryptedFsCreator.Create(ref outFileSystem, ref baseFileSystem.Ref(),
                        IEncryptedFileSystemCreator.KeyId.Save, in _encryptionSeed);
                    if (rc.IsFailure()) return rc.Miss();

                    return Result.Success;
                }

                case SaveDataSpaceId.ProperSystem:
                    rc = _config.BaseFsService.OpenBisFileSystem(ref baseFileSystem.Ref(),
                        BisPartitionId.SystemProperPartition, true);
                    if (rc.IsFailure()) return rc.Miss();

                    rc = Utility.WrapSubDirectory(ref outFileSystem, ref baseFileSystem.Ref(), in basePath, createIfMissing);
                    if (rc.IsFailure()) return rc.Miss();

                    return Result.Success;

                case SaveDataSpaceId.SafeMode:
                    rc = _config.BaseFsService.OpenBisFileSystem(ref baseFileSystem.Ref(), BisPartitionId.SafeMode, true);
                    if (rc.IsFailure()) return rc.Miss();

                    rc = Utility.WrapSubDirectory(ref outFileSystem, ref baseFileSystem.Ref(), in basePath, createIfMissing);
                    if (rc.IsFailure()) return rc.Miss();

                    return Result.Success;

                default:
                    return ResultFs.InvalidArgument.Log();
            }
        }

        public Result SetSdCardEncryptionSeed(in EncryptionSeed seed)
        {
            _encryptionSeed = seed;

            _config.SaveFsCreator.SetSdCardEncryptionSeed(seed.Value);
            _config.SaveIndexerManager.InvalidateIndexer(SaveDataSpaceId.SdSystem);
            _config.SaveIndexerManager.InvalidateIndexer(SaveDataSpaceId.SdCache);

            return Result.Success;
        }

        public Result IsProvisionallyCommittedSaveData(out bool isProvisionallyCommitted, in SaveDataInfo saveInfo)
        {
            throw new NotImplementedException();
        }

        public bool IsAllowedDirectorySaveData(SaveDataSpaceId spaceId, in Path saveDataRootPath)
        {
            return spaceId == SaveDataSpaceId.User && IsSaveEmulated(in saveDataRootPath);
        }

        // Todo: remove once file save data is supported
        // Used to always allow directory save data in OpenSaveDataFileSystem
        public bool IsAllowedDirectorySaveData2(SaveDataSpaceId spaceId, in Path saveDataRootPath)
        {
            // Todo: remove "|| true" once file save data is supported
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            return spaceId == SaveDataSpaceId.User && IsSaveEmulated(in saveDataRootPath) || true;
        }

        public bool IsDeviceUniqueMac(SaveDataSpaceId spaceId)
        {
            return spaceId == SaveDataSpaceId.System ||
                   spaceId == SaveDataSpaceId.User ||
                   spaceId == SaveDataSpaceId.Temporary ||
                   spaceId == SaveDataSpaceId.ProperSystem ||
                   spaceId == SaveDataSpaceId.SafeMode;
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
        /// Gets the program ID of the save data associated with the specified programID.
        /// </summary>
        /// <remarks>In a standard application the program ID will be the same as the input program ID.
        /// In multi-program applications all sub-programs use the program ID of the main program
        /// for their save data. The main program always has a program index of 0.</remarks>
        /// <param name="programId">The program ID to get the save data program ID for.</param>
        /// <returns>The program ID of the save data.</returns>
        public ProgramId ResolveDefaultSaveDataReferenceProgramId(ProgramId programId)
        {
            // First check if there's an entry in the program index map with the program ID and program index 0
            ProgramId mainProgramId = _config.ProgramRegistryService.GetProgramIdByIndex(programId, 0);

            if (mainProgramId != ProgramId.InvalidId)
            {
                return mainProgramId;
            }

            // Check if there's an entry with the program ID, ignoring program index
            Optional<ProgramIndexMapInfo> mapInfo = _config.ProgramRegistryService.GetProgramIndexMapInfo(programId);

            if (mapInfo.HasValue)
            {
                return mapInfo.Value.MainProgramId;
            }

            // The program ID isn't in the program index map. Probably running a single-program application
            return programId;
        }

        public Result GetSaveDataIndexCount(out int count)
        {
            UnsafeHelpers.SkipParamInit(out count);

            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

            Result rc = OpenSaveDataIndexerAccessor(ref accessor.Ref(), out bool _, SaveDataSpaceId.User);
            if (rc.IsFailure()) return rc.Miss();

            count = accessor.Get.Indexer.GetIndexCount();
            return Result.Success;
        }

        public Result OpenSaveDataIndexerAccessor(ref UniqueRef<SaveDataIndexerAccessor> outAccessor, out bool neededInit,
            SaveDataSpaceId spaceId)
        {
            return _config.SaveIndexerManager.OpenSaveDataIndexerAccessor(ref outAccessor, out neededInit, spaceId);
        }

        public void ResetTemporaryStorageIndexer()
        {
            _config.SaveIndexerManager.ResetIndexer(SaveDataSpaceId.Temporary);
        }
    }
}