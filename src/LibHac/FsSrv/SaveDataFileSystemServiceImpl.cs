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

            ReferenceCountedDisposable<IFileSystem> fileSystem = null;
            try
            {
                Result rc = OpenSaveDataDirectoryFileSystem(out fileSystem, spaceId);
                if (rc.IsFailure()) return rc;

                // Get the path of the save data
                // Hack around error CS8350.
                const int bufferLength = 0x12;
                Span<byte> buffer = stackalloc byte[bufferLength];
                ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
                Span<byte> saveImageNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

                var saveImageName = new Path();
                rc = PathFunctions.SetUpFixedPathSaveId(ref saveImageName, saveImageNameBuffer, saveDataId);
                if (rc.IsFailure()) return rc;

                rc = fileSystem.Target.GetEntryType(out _, in saveImageName);

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
                    return rc;
                }
            }
            finally
            {
                fileSystem?.Dispose();
            }
        }

        public Result OpenSaveDataFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            SaveDataSpaceId spaceId, ulong saveDataId, in Path saveDataRootPath, bool openReadOnly, SaveDataType type,
            bool cacheExtraData)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            ReferenceCountedDisposable<IFileSystem> saveDirectoryFs = null;
            ReferenceCountedDisposable<IFileSystem> cachedSaveDataFs = null;
            ReferenceCountedDisposable<IFileSystem> saveDataFs = null;
            try
            {
                Result rc = OpenSaveDataDirectoryFileSystem(out saveDirectoryFs, spaceId, in saveDataRootPath, true);
                if (rc.IsFailure()) return rc;

                bool allowDirectorySaveData = IsAllowedDirectorySaveData2(spaceId, in saveDataRootPath);

                // Note: When directory save data is allowed, Nintendo creates the save directory if it doesn't exist.
                // This bypasses normal save data creation, leaving the save with empty extra data.
                // Instead, we return that the save doesn't exist if the directory is missing.

                // Note: Nintendo doesn't cache directory save data
                // if (!allowDirectorySaveData)
                {
                    // Check if we have the requested file system cached
                    if (_saveDataFsCacheManager.GetCache(out cachedSaveDataFs, spaceId, saveDataId))
                    {
                        saveDataFs =
                            SaveDataFileSystemCacheRegisterBase<IFileSystem>.CreateShared(cachedSaveDataFs,
                                _saveDataFsCacheManager);

                        saveDataFs = SaveDataResultConvertFileSystem.CreateShared(ref saveDataFs);
                    }
                }

                // Create a new file system if it's not in the cache
                if (saveDataFs is null)
                {
                    ReferenceCountedDisposable<IFileSystem> saveFs = null;
                    ReferenceCountedDisposable<ISaveDataExtraDataAccessor> extraDataAccessor = null;
                    try
                    {
                        using ScopedLock<SdkRecursiveMutexType> scopedLock = _extraDataCacheManager.GetScopedLock();

                        bool openShared = SaveDataProperties.IsSharedOpenNeeded(type);
                        bool isMultiCommitSupported = SaveDataProperties.IsMultiCommitSupported(type);
                        bool isJournalingSupported = SaveDataProperties.IsJournalingSupported(type);
                        bool useDeviceUniqueMac = IsDeviceUniqueMac(spaceId);

                        rc = _config.SaveFsCreator.Create(out saveFs, out extraDataAccessor, _saveDataFsCacheManager,
                            ref saveDirectoryFs, spaceId, saveDataId, allowDirectorySaveData, useDeviceUniqueMac,
                            isJournalingSupported, isMultiCommitSupported, openReadOnly, openShared, _timeStampGetter);
                        if (rc.IsFailure()) return rc;

                        saveDataFs = Shared.Move(ref saveFs);

                        // Cache the extra data accessor if needed
                        if (cacheExtraData && extraDataAccessor is not null)
                        {
                            extraDataAccessor.Target.RegisterCacheObserver(_extraDataCacheManager, spaceId, saveDataId);

                            rc = _extraDataCacheManager.Register(extraDataAccessor, spaceId, saveDataId);
                            if (rc.IsFailure()) return rc;
                        }
                    }
                    finally
                    {
                        saveFs?.Dispose();
                        extraDataAccessor?.Dispose();
                    }
                }

                if (openReadOnly)
                {
                    saveDataFs = ReadOnlyFileSystem.CreateShared(ref saveDataFs);
                }

                Shared.Move(out fileSystem, ref saveDataFs);
                return Result.Success;
            }
            finally
            {
                saveDirectoryFs?.Dispose();
                cachedSaveDataFs?.Dispose();
                saveDataFs?.Dispose();
            }
        }

        public Result OpenSaveDataMetaDirectoryFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            // Hack around error CS8350.
            const int bufferLength = 0x1B;
            Span<byte> buffer = stackalloc byte[bufferLength];
            ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
            Span<byte> saveDataMetaIdDirectoryNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

            var saveDataMetaIdDirectoryName = new Path();
            Result rc = PathFunctions.SetUpFixedPathSaveMetaDir(ref saveDataMetaIdDirectoryName,
                saveDataMetaIdDirectoryNameBuffer, saveDataId);
            if (rc.IsFailure()) return rc;

            return OpenSaveDataDirectoryFileSystemImpl(out fileSystem, spaceId, in saveDataMetaIdDirectoryName);
        }

        public Result OpenSaveDataInternalStorageFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem,
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
            long metaSize)
        {
            ReferenceCountedDisposable<IFileSystem> metaDirFs = null;
            try
            {
                Result rc = OpenSaveDataMetaDirectoryFileSystem(out metaDirFs, spaceId, saveDataId);
                if (rc.IsFailure()) return rc;

                // Hack around error CS8350.
                const int bufferLength = 0xF;
                Span<byte> buffer = stackalloc byte[bufferLength];
                ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
                Span<byte> saveDataMetaNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

                var saveDataMetaName = new Path();
                rc = PathFunctions.SetUpFixedPathSaveMetaName(ref saveDataMetaName, saveDataMetaNameBuffer,
                    (uint)metaType);
                if (rc.IsFailure()) return rc;

                return metaDirFs.Target.CreateFile(in saveDataMetaName, metaSize);
            }
            finally
            {
                metaDirFs?.Dispose();
            }
        }

        public Result DeleteSaveDataMeta(ulong saveDataId, SaveDataSpaceId spaceId, SaveDataMetaType metaType)
        {
            ReferenceCountedDisposable<IFileSystem> metaDirFs = null;
            try
            {
                Result rc = OpenSaveDataMetaDirectoryFileSystem(out metaDirFs, spaceId, saveDataId);
                if (rc.IsFailure()) return rc;

                // Hack around error CS8350.
                const int bufferLength = 0xF;
                Span<byte> buffer = stackalloc byte[bufferLength];
                ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
                Span<byte> saveDataMetaNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

                var saveDataMetaName = new Path();
                rc = PathFunctions.SetUpFixedPathSaveMetaName(ref saveDataMetaName, saveDataMetaNameBuffer,
                    (uint)metaType);
                if (rc.IsFailure()) return rc;

                return metaDirFs.Target.DeleteFile(in saveDataMetaName);
            }
            finally
            {
                metaDirFs?.Dispose();
            }
        }

        public Result DeleteAllSaveDataMetas(ulong saveDataId, SaveDataSpaceId spaceId)
        {
            ReadOnlySpan<byte> metaDirName = // /saveMeta
                new[]
                {
                    (byte) '/', (byte) 's', (byte) 'a', (byte) 'v', (byte) 'e', (byte) 'M', (byte) 'e', (byte) 't',
                    (byte) 'a'
                };

            // Hack around error CS8350.
            const int bufferLength = 0x12;
            Span<byte> buffer = stackalloc byte[bufferLength];
            ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
            Span<byte> saveDataIdDirectoryNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

            ReferenceCountedDisposable<IFileSystem> fileSystem = null;
            try
            {
                var saveDataMetaDirectoryName = new Path();
                Result rc = PathFunctions.SetUpFixedPath(ref saveDataMetaDirectoryName, metaDirName);
                if (rc.IsFailure()) return rc;

                rc = OpenSaveDataDirectoryFileSystemImpl(out fileSystem, spaceId, in saveDataMetaDirectoryName, false);
                if (rc.IsFailure()) return rc;

                var saveDataIdDirectoryName = new Path();
                PathFunctions.SetUpFixedPathSaveId(ref saveDataIdDirectoryName, saveDataIdDirectoryNameBuffer,
                    saveDataId);
                if (rc.IsFailure()) return rc;

                // Delete the save data's meta directory, ignoring the error if the directory is already gone
                rc = fileSystem.Target.DeleteDirectoryRecursively(in saveDataIdDirectoryName);

                if (rc.IsFailure() && !ResultFs.PathNotFound.Includes(rc))
                    return rc;

                saveDataMetaDirectoryName.Dispose();
                saveDataIdDirectoryName.Dispose();

                return Result.Success;
            }
            finally
            {
                fileSystem?.Dispose();
            }
        }

        public Result OpenSaveDataMeta(out IFile metaFile, ulong saveDataId, SaveDataSpaceId spaceId,
            SaveDataMetaType metaType)
        {
            UnsafeHelpers.SkipParamInit(out metaFile);

            ReferenceCountedDisposable<IFileSystem> metaDirFs = null;
            try
            {
                Result rc = OpenSaveDataMetaDirectoryFileSystem(out metaDirFs, spaceId, saveDataId);
                if (rc.IsFailure()) return rc;

                // Hack around error CS8350.
                const int bufferLength = 0xF;
                Span<byte> buffer = stackalloc byte[bufferLength];
                ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
                Span<byte> saveDataMetaNameBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

                var saveDataMetaName = new Path();
                rc = PathFunctions.SetUpFixedPathSaveMetaName(ref saveDataMetaName, saveDataMetaNameBuffer,
                    (uint)metaType);
                if (rc.IsFailure()) return rc;

                return metaDirFs.Target.OpenFile(out metaFile, in saveDataMetaName, OpenMode.ReadWrite);
            }
            finally
            {
                metaDirFs?.Dispose();
            }
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

            ReferenceCountedDisposable<IFileSystem> saveDirectoryFs = null;
            try
            {
                Result rc = OpenSaveDataDirectoryFileSystem(out saveDirectoryFs, creationInfo.SpaceId,
                    in saveDataRootPath, false);
                if (rc.IsFailure()) return rc;

                var saveImageName = new Path();
                rc = PathFunctions.SetUpFixedPathSaveId(ref saveImageName, saveImageNameBuffer, saveDataId);
                if (rc.IsFailure()) return rc;

                if (_config.IsPseudoSaveData())
                {
                    rc = Utility12.EnsureDirectory(saveDirectoryFs.Target, in saveImageName);
                    if (rc.IsFailure()) return rc;

                    ReferenceCountedDisposable<IFileSystem> saveFs = null;
                    ReferenceCountedDisposable<ISaveDataExtraDataAccessor> extraDataAccessor = null;
                    try
                    {
                        bool isJournalingSupported = SaveDataProperties.IsJournalingSupported(attribute.Type);

                        rc = _config.SaveFsCreator.Create(out saveFs, out extraDataAccessor, _saveDataFsCacheManager,
                            ref saveDirectoryFs, creationInfo.SpaceId, saveDataId, allowDirectorySaveData: true,
                            useDeviceUniqueMac: false, isJournalingSupported, isMultiCommitSupported: false,
                            openReadOnly: false, openShared: false, _timeStampGetter);
                        if (rc.IsFailure()) return rc;

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

                        rc = extraDataAccessor.Target.WriteExtraData(in extraData);
                        if (rc.IsFailure()) return rc;

                        return extraDataAccessor.Target.CommitExtraData(true);
                    }
                    finally
                    {
                        saveFs?.Dispose();
                        extraDataAccessor?.Dispose();
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            finally
            {
                saveDirectoryFs?.Dispose();
            }
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

            ReferenceCountedDisposable<IFileSystem> fileSystem = null;
            try
            {
                _saveDataFsCacheManager.Unregister(spaceId, saveDataId);

                // Open the directory containing the save data
                Result rc = OpenSaveDataDirectoryFileSystem(out fileSystem, spaceId, in saveDataRootPath, false);
                if (rc.IsFailure()) return rc;

                var saveImageName = new Path();
                rc = PathFunctions.SetUpFixedPathSaveId(ref saveImageName, saveImageNameBuffer, saveDataId);
                if (rc.IsFailure()) return rc;

                // Check if the save data is a file or a directory
                rc = fileSystem.Target.GetEntryType(out DirectoryEntryType entryType, in saveImageName);
                if (rc.IsFailure()) return rc;

                // Delete the save data, wiping the file if needed
                if (entryType == DirectoryEntryType.Directory)
                {
                    rc = fileSystem.Target.DeleteDirectoryRecursively(in saveImageName);
                    if (rc.IsFailure()) return rc;
                }
                else
                {
                    if (wipeSaveFile)
                    {
                        WipeData(fileSystem.Target, in saveImageName, _config.GenerateRandomData).IgnoreResult();
                    }

                    rc = fileSystem.Target.DeleteFile(in saveImageName);
                    if (rc.IsFailure()) return rc;
                }

                saveImageName.Dispose();
                return Result.Success;
            }
            finally
            {
                fileSystem?.Dispose();
            }
        }

        public Result ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, SaveDataSpaceId spaceId,
            ulong saveDataId, SaveDataType type, in Path saveDataRootPath)
        {
            UnsafeHelpers.SkipParamInit(out extraData);

            // Nintendo returns blank extra data for directory save data.
            // We've extended directory save data to store extra data so we don't need to do that.

            using ScopedLock<SdkRecursiveMutexType> scopedLockFsCache = _saveDataFsCacheManager.GetScopedLock();
            using ScopedLock<SdkRecursiveMutexType> scopedLockExtraDataCache = _extraDataCacheManager.GetScopedLock();

            ReferenceCountedDisposable<ISaveDataExtraDataAccessor> extraDataAccessor = null;
            try
            {
                // Try to grab an extra data accessor for the requested save from the cache.
                Result rc = _extraDataCacheManager.GetCache(out extraDataAccessor, spaceId, saveDataId);

                if (rc.IsSuccess())
                {
                    // An extra data accessor was found in the cache. Read the extra data from it.
                    return extraDataAccessor.Target.ReadExtraData(out extraData);
                }

                ReferenceCountedDisposable<IFileSystem> unusedSaveDataFs = null;
                try
                {
                    // We won't actually use the returned save data FS.
                    // Opening the FS should cache an extra data accessor for it.
                    rc = OpenSaveDataFileSystem(out unusedSaveDataFs, spaceId, saveDataId, saveDataRootPath,
                        openReadOnly: true, type, cacheExtraData: true);
                    if (rc.IsFailure()) return rc;

                    // Try to grab an accessor from the cache again.
                    rc = _extraDataCacheManager.GetCache(out extraDataAccessor, spaceId, saveDataId);

                    if (rc.IsFailure())
                    {
                        // No extra data accessor was registered for the requested save data.
                        // Return a blank extra data struct.
                        extraData = new SaveDataExtraData();
                        return rc;
                    }

                    return extraDataAccessor.Target.ReadExtraData(out extraData);
                }
                finally
                {
                    unusedSaveDataFs?.Dispose();
                }
            }
            finally
            {
                extraDataAccessor?.Dispose();
            }
        }

        public Result WriteSaveDataFileSystemExtraData(SaveDataSpaceId spaceId, ulong saveDataId,
            in SaveDataExtraData extraData, in Path saveDataRootPath, SaveDataType type, bool updateTimeStamp)
        {
            // Nintendo does nothing when writing directory save data extra data.
            // We've extended directory save data to store extra data so we don't return early.

            using ScopedLock<SdkRecursiveMutexType> scopedLockFsCache = _saveDataFsCacheManager.GetScopedLock();
            using ScopedLock<SdkRecursiveMutexType> scopedLockExtraDataCache = _extraDataCacheManager.GetScopedLock();

            ReferenceCountedDisposable<ISaveDataExtraDataAccessor> extraDataAccessor = null;
            try
            {
                // Try to grab an extra data accessor for the requested save from the cache.
                Result rc = _extraDataCacheManager.GetCache(out extraDataAccessor, spaceId, saveDataId);

                if (rc.IsFailure())
                {
                    // No accessor was found in the cache. Try to open one.
                    ReferenceCountedDisposable<IFileSystem> unusedSaveDataFs = null;
                    try
                    {
                        // We won't actually use the returned save data FS.
                        // Opening the FS should cache an extra data accessor for it.
                        rc = OpenSaveDataFileSystem(out unusedSaveDataFs, spaceId, saveDataId, saveDataRootPath,
                            openReadOnly: false, type, cacheExtraData: true);
                        if (rc.IsFailure()) return rc;

                        // Try to grab an accessor from the cache again.
                        rc = _extraDataCacheManager.GetCache(out extraDataAccessor, spaceId, saveDataId);

                        if (rc.IsFailure())
                        {
                            // No extra data accessor was registered for the requested save data, so don't do anything.
                            return rc;
                        }
                    }
                    finally
                    {
                        unusedSaveDataFs?.Dispose();
                    }
                }

                // We should have a valid accessor if we've reached this point.
                // Write and commit the extra data.
                rc = extraDataAccessor.Target.WriteExtraData(in extraData);
                if (rc.IsFailure()) return rc;

                rc = extraDataAccessor.Target.CommitExtraData(updateTimeStamp);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
            finally
            {
                extraDataAccessor?.Dispose();
            }
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

        public Result OpenSaveDataDirectoryFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            SaveDataSpaceId spaceId)
        {
            var rootPath = new Path();

            return OpenSaveDataDirectoryFileSystem(out fileSystem, spaceId, in rootPath, true);
        }

        public Result OpenSaveDataDirectoryFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            SaveDataSpaceId spaceId, in Path saveDataRootPath, bool allowEmulatedSave)
        {
            Result rc;
            UnsafeHelpers.SkipParamInit(out fileSystem);

            if (allowEmulatedSave && IsAllowedDirectorySaveData(spaceId, in saveDataRootPath))
            {
                ReferenceCountedDisposable<IFileSystem> tmFs = null;

                try
                {
                    // Ensure the target save data directory exists
                    rc = _config.TargetManagerFsCreator.Create(out tmFs, in saveDataRootPath, false, true,
                        ResultFs.SaveDataRootPathUnavailable.Value);
                    if (rc.IsFailure()) return rc;

                    tmFs.Dispose();

                    var path = new Path();
                    rc = path.Initialize(in saveDataRootPath);
                    if (rc.IsFailure()) return rc;

                    rc = _config.TargetManagerFsCreator.NormalizeCaseOfPath(out bool isTargetFsCaseSensitive, ref path);
                    if (rc.IsFailure()) return rc;

                    rc = _config.TargetManagerFsCreator.Create(out tmFs, in path, isTargetFsCaseSensitive, false,
                        ResultFs.SaveDataRootPathUnavailable.Value);
                    if (rc.IsFailure()) return rc;

                    path.Dispose();
                    return Result.Success;
                }
                finally
                {
                    tmFs?.Dispose();
                }
            }

            var saveDataDirPath = new Path();
            ReadOnlySpan<byte> saveDirName;

            if (spaceId == SaveDataSpaceId.Temporary)
            {
                saveDirName = new[] { (byte)'/', (byte)'t', (byte)'e', (byte)'m', (byte)'p' }; // /temp
            }
            else
            {
                saveDirName = new[] { (byte)'/', (byte)'s', (byte)'a', (byte)'v', (byte)'e' }; // /save
            }

            rc = PathFunctions.SetUpFixedPath(ref saveDataDirPath, saveDirName);
            if (rc.IsFailure()) return rc;

            rc = OpenSaveDataDirectoryFileSystemImpl(out fileSystem, spaceId, in saveDataDirPath, true);
            if (rc.IsFailure()) return rc;

            saveDataDirPath.Dispose();
            return Result.Success;
        }

        public Result OpenSaveDataDirectoryFileSystemImpl(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            SaveDataSpaceId spaceId, in Path basePath)
        {
            return OpenSaveDataDirectoryFileSystemImpl(out fileSystem, spaceId, in basePath, true);
        }

        public Result OpenSaveDataDirectoryFileSystemImpl(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            SaveDataSpaceId spaceId, in Path basePath, bool createIfMissing)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            ReferenceCountedDisposable<IFileSystem> baseFileSystem = null;
            ReferenceCountedDisposable<IFileSystem> tempFileSystem = null;
            try
            {
                Result rc;

                switch (spaceId)
                {
                    case SaveDataSpaceId.System:
                        rc = _config.BaseFsService.OpenBisFileSystem(out baseFileSystem, BisPartitionId.System, true);
                        if (rc.IsFailure()) return rc;

                        return Utility.WrapSubDirectory(out fileSystem, ref baseFileSystem, in basePath, createIfMissing);

                    case SaveDataSpaceId.User:
                    case SaveDataSpaceId.Temporary:
                        rc = _config.BaseFsService.OpenBisFileSystem(out baseFileSystem, BisPartitionId.User, true);
                        if (rc.IsFailure()) return rc;

                        return Utility.WrapSubDirectory(out fileSystem, ref baseFileSystem, in basePath, createIfMissing);

                    case SaveDataSpaceId.SdSystem:
                    case SaveDataSpaceId.SdCache:
                        rc = _config.BaseFsService.OpenSdCardProxyFileSystem(out baseFileSystem, true);
                        if (rc.IsFailure()) return rc;

                        // Hack around error CS8350.
                        const int bufferLength = 0x40;
                        Span<byte> buffer = stackalloc byte[bufferLength];
                        ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
                        Span<byte> pathParentBuffer = MemoryMarshal.CreateSpan(ref bufferRef, bufferLength);

                        var parentPath = new Path();
                        rc = PathFunctions.SetUpFixedPathSingleEntry(ref parentPath, pathParentBuffer,
                            CommonPaths.SdCardNintendoRootDirectoryName);
                        if (rc.IsFailure()) return rc;

                        var pathSdRoot = new Path();
                        rc = pathSdRoot.Combine(in parentPath, in basePath);
                        if (rc.IsFailure()) return rc;

                        tempFileSystem = Shared.Move(ref baseFileSystem);
                        rc = Utility.WrapSubDirectory(out baseFileSystem, ref tempFileSystem, in pathSdRoot,
                            createIfMissing);
                        if (rc.IsFailure()) return rc;

                        rc = _config.EncryptedFsCreator.Create(out fileSystem, ref baseFileSystem,
                            IEncryptedFileSystemCreator.KeyId.Save, in _encryptionSeed);
                        if (rc.IsFailure()) return rc;

                        parentPath.Dispose();
                        pathSdRoot.Dispose();
                        return Result.Success;

                    case SaveDataSpaceId.ProperSystem:
                        rc = _config.BaseFsService.OpenBisFileSystem(out baseFileSystem,
                            BisPartitionId.SystemProperPartition, true);
                        if (rc.IsFailure()) return rc;

                        return Utility.WrapSubDirectory(out fileSystem, ref baseFileSystem, in basePath, createIfMissing);

                    case SaveDataSpaceId.SafeMode:
                        rc = _config.BaseFsService.OpenBisFileSystem(out baseFileSystem, BisPartitionId.SafeMode, true);
                        if (rc.IsFailure()) return rc;

                        return Utility.WrapSubDirectory(out fileSystem, ref baseFileSystem, in basePath, createIfMissing);

                    default:
                        return ResultFs.InvalidArgument.Log();
                }
            }
            finally
            {
                baseFileSystem?.Dispose();
                tempFileSystem?.Dispose();
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

            SaveDataIndexerAccessor accessor = null;
            try
            {
                Result rc = OpenSaveDataIndexerAccessor(out accessor, out bool _, SaveDataSpaceId.User);
                if (rc.IsFailure()) return rc;

                count = accessor.Indexer.GetIndexCount();
                return Result.Success;
            }
            finally
            {
                accessor?.Dispose();
            }
        }

        public Result OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, out bool neededInit,
            SaveDataSpaceId spaceId)
        {
            return _config.SaveIndexerManager.OpenSaveDataIndexerAccessor(out accessor, out neededInit, spaceId);
        }

        public void ResetTemporaryStorageIndexer()
        {
            _config.SaveIndexerManager.ResetIndexer(SaveDataSpaceId.Temporary);
        }
    }
}