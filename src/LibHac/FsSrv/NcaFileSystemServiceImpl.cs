using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;
using LibHac.FsSystem;
using LibHac.FsSystem.NcaUtils;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Spl;
using LibHac.Util;
using RightsId = LibHac.Fs.RightsId;

namespace LibHac.FsSrv
{
    public class NcaFileSystemServiceImpl
    {
        private Configuration _config;
        // UpdatePartitionPath
        private ExternalKeySet _externalKeyManager;
        private LocationResolverSet _locationResolverSet;
        // SystemDataUpdateEventManager
        private EncryptionSeed _encryptionSeed;
        private int _romFsRemountForDataCorruptionCount;
        private int _romfsUnrecoverableDataCorruptionByRemountCount;
        private int _romFsRecoveredByInvalidateCacheCount;
        private SdkMutexType _romfsCountMutex;

        public NcaFileSystemServiceImpl(in Configuration configuration, ExternalKeySet externalKeySet)
        {
            _config = configuration;
            _externalKeyManager = externalKeySet;
            _locationResolverSet = new LocationResolverSet(_config.FsServer);
            _romfsCountMutex.Initialize();
        }

        public struct Configuration
        {
            public BaseFileSystemServiceImpl BaseFsService;
            public ILocalFileSystemCreator LocalFsCreator;
            public ITargetManagerFileSystemCreator TargetManagerFsCreator;
            public IPartitionFileSystemCreator PartitionFsCreator;
            public IRomFileSystemCreator RomFsCreator;
            public IStorageOnNcaCreator StorageOnNcaCreator;
            public ISubDirectoryFileSystemCreator SubDirectoryFsCreator;
            public IEncryptedFileSystemCreator EncryptedFsCreator;
            public ProgramRegistryServiceImpl ProgramRegistryService;
            public AccessFailureManagementServiceImpl AccessFailureManagementService;
            public InternalProgramIdRangeForSpeedEmulation SpeedEmulationRange;

            // LibHac additions
            public FileSystemServer FsServer;
        }

        private struct MountInfo
        {
            public bool IsGameCard;
            public int GcHandle;
            public bool IsHostFs;
            public bool CanMountNca;
        }

        public Result OpenFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem, in Path path,
            FileSystemProxyType type, ulong id, bool isDirectory)
        {
            return OpenFileSystem(out fileSystem, out Unsafe.NullRef<CodeVerificationData>(), in path, type, false, id,
                isDirectory);
        }

        public Result OpenFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem, in Path path,
            FileSystemProxyType type, bool canMountSystemDataPrivate, ulong id, bool isDirectory)
        {
            return OpenFileSystem(out fileSystem, out Unsafe.NullRef<CodeVerificationData>(), in path, type,
                canMountSystemDataPrivate, id, isDirectory);
        }

        public Result OpenFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            out CodeVerificationData verificationData, in Path path, FileSystemProxyType type,
            bool canMountSystemDataPrivate, ulong id, bool isDirectory)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem, out verificationData);

            if (!Unsafe.IsNullRef(ref verificationData))
                verificationData.IsValid = false;

            // Get a reference to the path that will be advanced as each part of the path is parsed
            var currentPath = new U8Span(path.GetString());

            // Open the root filesystem based on the path's mount name
            Result rc = ParseMountName(ref currentPath,
                out ReferenceCountedDisposable<IFileSystem> baseFileSystem, out bool shouldContinue,
                out MountInfo mountNameInfo);
            if (rc.IsFailure()) return rc;

            // Don't continue if the rest of the path is empty
            if (!shouldContinue)
                return ResultFs.InvalidArgument.Log();

            if (type == FileSystemProxyType.Logo && mountNameInfo.IsGameCard)
            {
                rc = _config.BaseFsService.OpenGameCardFileSystem(out fileSystem,
                    new GameCardHandle(mountNameInfo.GcHandle),
                    GameCardPartition.Logo);

                if (rc.IsSuccess())
                    return Result.Success;

                if (!ResultFs.PartitionNotFound.Includes(rc))
                    return rc;
            }

            rc = CheckDirOrNcaOrNsp(ref currentPath, out isDirectory);
            if (rc.IsFailure()) return rc;

            if (isDirectory)
            {
                if (!mountNameInfo.IsHostFs)
                    return ResultFs.PermissionDenied.Log();

                using var directoryPath = new Path();
                rc = directoryPath.InitializeWithNormalization(currentPath.Value);
                if (rc.IsFailure()) return rc;

                if (type == FileSystemProxyType.Manual)
                {
                    ReferenceCountedDisposable<IFileSystem> hostFileSystem = null;
                    ReferenceCountedDisposable<IFileSystem> readOnlyFileSystem = null;
                    try
                    {
                        rc = ParseDirWithPathCaseNormalizationOnCaseSensitiveHostFs(out hostFileSystem,
                            in directoryPath);
                        if (rc.IsFailure()) return rc;

                        readOnlyFileSystem = ReadOnlyFileSystem.CreateShared(ref hostFileSystem);
                        if (readOnlyFileSystem?.Target is null)
                            return ResultFs.AllocationMemoryFailedAllocateShared.Log();

                        Shared.Move(out fileSystem, ref readOnlyFileSystem);
                        return Result.Success;
                    }
                    finally
                    {
                        hostFileSystem?.Dispose();
                        readOnlyFileSystem?.Dispose();
                    }
                }

                return ParseDir(in directoryPath, out fileSystem, ref baseFileSystem, type, true);
            }

            rc = ParseNsp(ref currentPath, out ReferenceCountedDisposable<IFileSystem> nspFileSystem, baseFileSystem);

            if (rc.IsSuccess())
            {
                // Must be the end of the path to open Application Package FS type
                if (currentPath.Value.At(0) == 0)
                {
                    if (type == FileSystemProxyType.Package)
                    {
                        fileSystem = nspFileSystem;
                        return Result.Success;
                    }

                    return ResultFs.InvalidArgument.Log();
                }

                baseFileSystem = nspFileSystem;
            }

            if (!mountNameInfo.CanMountNca)
            {
                return ResultFs.UnexpectedInNcaFileSystemServiceImplA.Log();
            }

            ulong openProgramId = mountNameInfo.IsHostFs ? ulong.MaxValue : id;

            rc = ParseNca(ref currentPath, out Nca nca, baseFileSystem, openProgramId);
            if (rc.IsFailure()) return rc;

            rc = OpenStorageByContentType(out ReferenceCountedDisposable<IStorage> ncaSectionStorage, nca,
                out NcaFormatType fsType, type, mountNameInfo.IsGameCard, canMountSystemDataPrivate);
            if (rc.IsFailure()) return rc;

            switch (fsType)
            {
                case NcaFormatType.Romfs:
                    return _config.RomFsCreator.Create(out fileSystem, ncaSectionStorage);
                case NcaFormatType.Pfs0:
                    return _config.PartitionFsCreator.Create(out fileSystem, ncaSectionStorage);
                default:
                    return ResultFs.InvalidNcaFileSystemType.Log();
            }
        }

        public Result OpenDataFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem, in Path path,
            FileSystemProxyType fsType, ulong programId, bool isDirectory)
        {
            throw new NotImplementedException();
        }

        public Result OpenStorageWithPatch(out ReferenceCountedDisposable<IStorage> storage, out Hash ncaHeaderDigest,
            in Path originalNcaPath, in Path currentNcaPath, FileSystemProxyType fsType, ulong id)
        {
            throw new NotImplementedException();
        }

        public Result OpenFileSystemWithPatch(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            in Path originalNcaPath, in Path currentNcaPath, FileSystemProxyType fsType, ulong id)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            ReferenceCountedDisposable<IStorage> romFsStorage = null;
            try
            {
                Result rc = OpenStorageWithPatch(out romFsStorage, out Unsafe.NullRef<Hash>(), in originalNcaPath,
                    in currentNcaPath, fsType, id);
                if (rc.IsFailure()) return rc;

                return _config.RomFsCreator.Create(out fileSystem, romFsStorage);
            }
            finally
            {
                romFsStorage?.Dispose();
            }
        }

        public Result OpenContentStorageFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            ContentStorageId contentStorageId)
        {
            const int pathBufferLength = 0x40;

            UnsafeHelpers.SkipParamInit(out fileSystem);

            ReferenceCountedDisposable<IFileSystem> baseFileSystem = null;
            ReferenceCountedDisposable<IFileSystem> subDirFileSystem = null;
            ReferenceCountedDisposable<IFileSystem> tempFileSystem = null;

            try
            {
                Result rc;

                // Open the appropriate base file system for the content storage ID
                switch (contentStorageId)
                {
                    case ContentStorageId.System:
                        rc = _config.BaseFsService.OpenBisFileSystem(out baseFileSystem, BisPartitionId.System);
                        if (rc.IsFailure()) return rc;
                        break;
                    case ContentStorageId.User:
                        rc = _config.BaseFsService.OpenBisFileSystem(out baseFileSystem, BisPartitionId.User);
                        if (rc.IsFailure()) return rc;
                        break;
                    case ContentStorageId.SdCard:
                        rc = _config.BaseFsService.OpenSdCardProxyFileSystem(out baseFileSystem);
                        if (rc.IsFailure()) return rc;
                        break;
                    default:
                        return ResultFs.InvalidArgument.Log();
                }

                // Hack around error CS8350.
                Span<byte> buffer = stackalloc byte[pathBufferLength];
                ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
                Span<byte> contentStoragePathBuffer = MemoryMarshal.CreateSpan(ref bufferRef, pathBufferLength);

                // Build the appropriate path for the content storage ID
                if (contentStorageId == ContentStorageId.SdCard)
                {
                    var sb = new U8StringBuilder(contentStoragePathBuffer);
                    sb.Append(StringTraits.DirectorySeparator).Append(SdCardNintendoRootDirectoryName);
                    sb.Append(StringTraits.DirectorySeparator).Append(ContentStorageDirectoryName);
                }
                else
                {
                    var sb = new U8StringBuilder(contentStoragePathBuffer);
                    sb.Append(StringTraits.DirectorySeparator).Append(ContentStorageDirectoryName);
                }

                using var contentStoragePath = new Path();
                rc = PathFunctions.SetUpFixedPath(ref contentStoragePath.Ref(), contentStoragePathBuffer);
                if (rc.IsFailure()) return rc;

                // Make sure the content storage path exists
                rc = Utility12.EnsureDirectory(baseFileSystem.Target, in contentStoragePath);
                if (rc.IsFailure()) return rc;

                rc = _config.SubDirectoryFsCreator.Create(out subDirFileSystem, ref baseFileSystem,
                    in contentStoragePath);
                if (rc.IsFailure()) return rc;

                // Only content on the SD card is encrypted
                if (contentStorageId == ContentStorageId.SdCard)
                {
                    tempFileSystem = Shared.Move(ref subDirFileSystem);

                    rc = _config.EncryptedFsCreator.Create(out subDirFileSystem, ref tempFileSystem,
                       IEncryptedFileSystemCreator.KeyId.Content, in _encryptionSeed);
                    if (rc.IsFailure()) return rc;
                }

                Shared.Move(out fileSystem, ref subDirFileSystem);

                return Result.Success;
            }
            finally
            {
                baseFileSystem?.Dispose();
                subDirFileSystem?.Dispose();
                tempFileSystem?.Dispose();
            }
        }

        public Result GetRightsId(out RightsId rightsId, out byte keyGeneration, in Path path, ProgramId programId)
        {
            throw new NotImplementedException();
        }

        public Result RegisterExternalKey(in RightsId rightsId, in AccessKey accessKey)
        {
            return _externalKeyManager.Add(rightsId, accessKey);
        }

        public Result UnregisterExternalKey(in RightsId rightsId)
        {
            _externalKeyManager.Remove(rightsId);

            return Result.Success;
        }

        public Result UnregisterAllExternalKey()
        {
            _externalKeyManager.Clear();

            return Result.Success;
        }

        public Result RegisterUpdatePartition(ulong programId, in Path path)
        {
            throw new NotImplementedException();
        }

        public Result OpenRegisteredUpdatePartition(out ReferenceCountedDisposable<IFileSystem> fileSystem)
        {
            throw new NotImplementedException();
        }

        private Result ParseMountName(ref U8Span path,
            out ReferenceCountedDisposable<IFileSystem> fileSystem, out bool shouldContinue, out MountInfo info)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            info = new MountInfo();
            shouldContinue = true;

            if (StringUtils.Compare(path, CommonPaths.GameCardFileSystemMountName,
                CommonPaths.GameCardFileSystemMountName.Length) == 0)
            {
                path = path.Slice(CommonPaths.GameCardFileSystemMountName.Length);

                if (StringUtils.GetLength(path.Value, 9) < 9)
                    return ResultFs.InvalidPath.Log();

                GameCardPartition partition;
                switch ((char)path[0])
                {
                    case CommonPaths.GameCardFileSystemMountNameUpdateSuffix:
                        partition = GameCardPartition.Update;
                        break;
                    case CommonPaths.GameCardFileSystemMountNameNormalSuffix:
                        partition = GameCardPartition.Normal;
                        break;
                    case CommonPaths.GameCardFileSystemMountNameSecureSuffix:
                        partition = GameCardPartition.Secure;
                        break;
                    default:
                        return ResultFs.InvalidPath.Log();
                }

                path = path.Slice(1);
                bool handleParsed = Utf8Parser.TryParse(path, out int handle, out int bytesConsumed);

                if (!handleParsed || handle == -1 || bytesConsumed != 8)
                    return ResultFs.InvalidPath.Log();

                path = path.Slice(8);

                Result rc = _config.BaseFsService.OpenGameCardFileSystem(out fileSystem, new GameCardHandle(handle),
                    partition);
                if (rc.IsFailure()) return rc;

                info.GcHandle = handle;
                info.IsGameCard = true;
                info.CanMountNca = true;
            }

            else if (StringUtils.Compare(path, CommonPaths.ContentStorageSystemMountName,
                CommonPaths.ContentStorageSystemMountName.Length) == 0)
            {
                path = path.Slice(CommonPaths.ContentStorageSystemMountName.Length);

                Result rc = OpenContentStorageFileSystem(out fileSystem, ContentStorageId.System);
                if (rc.IsFailure()) return rc;

                info.CanMountNca = true;
            }

            else if (StringUtils.Compare(path, CommonPaths.ContentStorageUserMountName,
                CommonPaths.ContentStorageUserMountName.Length) == 0)
            {
                path = path.Slice(CommonPaths.ContentStorageUserMountName.Length);

                Result rc = OpenContentStorageFileSystem(out fileSystem, ContentStorageId.User);
                if (rc.IsFailure()) return rc;

                info.CanMountNca = true;
            }

            else if (StringUtils.Compare(path, CommonPaths.ContentStorageSdCardMountName,
                CommonPaths.ContentStorageSdCardMountName.Length) == 0)
            {
                path = path.Slice(CommonPaths.ContentStorageSdCardMountName.Length);

                Result rc = OpenContentStorageFileSystem(out fileSystem, ContentStorageId.SdCard);
                if (rc.IsFailure()) return rc;

                info.CanMountNca = true;
            }

            else if (StringUtils.Compare(path, CommonPaths.BisCalibrationFilePartitionMountName,
                CommonPaths.BisCalibrationFilePartitionMountName.Length) == 0)
            {
                path = path.Slice(CommonPaths.BisCalibrationFilePartitionMountName.Length);

                Result rc = _config.BaseFsService.OpenBisFileSystem(out fileSystem, BisPartitionId.CalibrationFile);
                if (rc.IsFailure()) return rc;
            }

            else if (StringUtils.Compare(path, CommonPaths.BisSafeModePartitionMountName,
                CommonPaths.BisSafeModePartitionMountName.Length) == 0)
            {
                path = path.Slice(CommonPaths.BisSafeModePartitionMountName.Length);

                Result rc = _config.BaseFsService.OpenBisFileSystem(out fileSystem, BisPartitionId.SafeMode);
                if (rc.IsFailure()) return rc;
            }

            else if (StringUtils.Compare(path, CommonPaths.BisUserPartitionMountName,
                CommonPaths.BisUserPartitionMountName.Length) == 0)
            {
                path = path.Slice(CommonPaths.BisUserPartitionMountName.Length);

                Result rc = _config.BaseFsService.OpenBisFileSystem(out fileSystem, BisPartitionId.User);
                if (rc.IsFailure()) return rc;
            }

            else if (StringUtils.Compare(path, CommonPaths.BisSystemPartitionMountName,
                CommonPaths.BisSystemPartitionMountName.Length) == 0)
            {
                path = path.Slice(CommonPaths.BisSystemPartitionMountName.Length);

                Result rc = _config.BaseFsService.OpenBisFileSystem(out fileSystem, BisPartitionId.System);
                if (rc.IsFailure()) return rc;
            }

            else if (StringUtils.Compare(path, CommonPaths.SdCardFileSystemMountName,
                CommonPaths.SdCardFileSystemMountName.Length) == 0)
            {
                path = path.Slice(CommonPaths.SdCardFileSystemMountName.Length);

                Result rc = _config.BaseFsService.OpenSdCardProxyFileSystem(out fileSystem);
                if (rc.IsFailure()) return rc;
            }

            else if (StringUtils.Compare(path, CommonPaths.HostRootFileSystemMountName,
                CommonPaths.HostRootFileSystemMountName.Length) == 0)
            {
                path = path.Slice(CommonPaths.HostRootFileSystemMountName.Length);

                using var rootPathEmpty = new Path();
                Result rc = rootPathEmpty.InitializeAsEmpty();
                if (rc.IsFailure()) return rc;

                info.IsHostFs = true;
                info.CanMountNca = true;

                rc = OpenHostFileSystem(out fileSystem, in rootPathEmpty, openCaseSensitive: false);
                if (rc.IsFailure()) return rc;
            }

            else if (StringUtils.Compare(path, CommonPaths.RegisteredUpdatePartitionMountName,
                CommonPaths.RegisteredUpdatePartitionMountName.Length) == 0)
            {
                path = path.Slice(CommonPaths.RegisteredUpdatePartitionMountName.Length);

                info.CanMountNca = true;

                throw new NotImplementedException();
            }

            else
            {
                return ResultFs.PathNotFound.Log();
            }

            if (StringUtils.GetLength(path, FsPath.MaxLength) == 0)
            {
                shouldContinue = false;
            }

            return Result.Success;
        }

        private Result CheckDirOrNcaOrNsp(ref U8Span path, out bool isDirectory)
        {
            UnsafeHelpers.SkipParamInit(out isDirectory);

            ReadOnlySpan<byte> mountSeparator = new[] { (byte)':', (byte)'/' };

            if (StringUtils.Compare(mountSeparator, path, mountSeparator.Length) != 0)
            {
                return ResultFs.PathNotFound.Log();
            }

            path = path.Slice(1);
            int pathLen = StringUtils.GetLength(path);

            if (path[pathLen - 1] == '/')
            {
                isDirectory = true;
                return Result.Success;
            }

            // Now make sure the path has a content file extension
            if (pathLen < 5)
                return ResultFs.PathNotFound.Log();

            ReadOnlySpan<byte> fileExtension = path.Value.Slice(pathLen - 4);

            ReadOnlySpan<byte> ncaExtension = new[] { (byte)'.', (byte)'n', (byte)'c', (byte)'a' };
            ReadOnlySpan<byte> nspExtension = new[] { (byte)'.', (byte)'n', (byte)'s', (byte)'p' };

            if (StringUtils.CompareCaseInsensitive(fileExtension, ncaExtension) == 0 ||
                StringUtils.CompareCaseInsensitive(fileExtension, nspExtension) == 0)
            {
                isDirectory = false;
                return Result.Success;
            }

            return ResultFs.PathNotFound.Log();
        }

        private Result ParseDir(in Path path,
            out ReferenceCountedDisposable<IFileSystem> contentFileSystem,
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, FileSystemProxyType fsType, bool preserveUnc)
        {
            UnsafeHelpers.SkipParamInit(out contentFileSystem);

            ReferenceCountedDisposable<IFileSystem> subDirFs = null;
            try
            {
                Result rc = _config.SubDirectoryFsCreator.Create(out subDirFs, ref baseFileSystem, in path);
                if (rc.IsFailure()) return rc;

                return ParseContentTypeForDirectory(out contentFileSystem, ref subDirFs, fsType);
            }
            finally
            {
                subDirFs?.Dispose();
            }
        }

        private Result ParseDirWithPathCaseNormalizationOnCaseSensitiveHostFs(
            out ReferenceCountedDisposable<IFileSystem> fileSystem, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            using var pathRoot = new Path();
            using var pathData = new Path();

            Result rc = PathFunctions.SetUpFixedPath(ref pathData.Ref(),
                new[] { (byte)'/', (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            if (rc.IsFailure()) return rc;

            rc = pathRoot.Combine(in path, in pathData);
            if (rc.IsFailure()) return rc;

            rc = _config.TargetManagerFsCreator.NormalizeCaseOfPath(out bool isSupported, ref pathRoot.Ref());
            if (rc.IsFailure()) return rc;

            rc = _config.TargetManagerFsCreator.Create(out fileSystem, in pathRoot, isSupported, false, Result.Success);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        private Result ParseNsp(ref U8Span path, out ReferenceCountedDisposable<IFileSystem> fileSystem,
            ReferenceCountedDisposable<IFileSystem> baseFileSystem)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            ReadOnlySpan<byte> nspExtension = new[] { (byte)'.', (byte)'n', (byte)'s', (byte)'p' };

            // Search for the end of the nsp part of the path
            int nspPathLen = 0;

            while (true)
            {
                U8Span currentSpan;

                while (true)
                {
                    currentSpan = path.Slice(nspPathLen);
                    if (StringUtils.CompareCaseInsensitive(nspExtension, currentSpan, 4) == 0)
                        break;

                    if (currentSpan.Length == 0 || currentSpan[0] == 0)
                    {
                        return ResultFs.PathNotFound.Log();
                    }

                    nspPathLen++;
                }

                // The nsp filename must be the end of the entire path or the end of a path segment
                if (currentSpan.Length <= 4 || currentSpan[4] == 0 || currentSpan[4] == (byte)'/')
                    break;

                nspPathLen += 4;
            }

            nspPathLen += 4;

            using var pathNsp = new Path();
            Result rc = pathNsp.InitializeWithNormalization(path, nspPathLen);
            if (rc.IsFailure()) return rc;

            var storage = new FileStorageBasedFileSystem();
            using var nspFileStorage = new ReferenceCountedDisposable<FileStorageBasedFileSystem>(storage);

            rc = nspFileStorage.Target.Initialize(ref baseFileSystem, in pathNsp, OpenMode.Read);
            if (rc.IsFailure()) return rc;

            rc = _config.PartitionFsCreator.Create(out fileSystem, nspFileStorage.AddReference<IStorage>());

            if (rc.IsSuccess())
            {
                path = path.Slice(nspPathLen);
            }

            return rc;
        }

        private Result ParseNca(ref U8Span path, out Nca nca, ReferenceCountedDisposable<IFileSystem> baseFileSystem,
            ulong ncaId)
        {
            UnsafeHelpers.SkipParamInit(out nca);

            // Todo: Create ref-counted storage
            var ncaFileStorage = new FileStorageBasedFileSystem();

            using var pathNca = new Path();
            Result rc = pathNca.InitializeWithNormalization(path);
            if (rc.IsFailure()) return rc;

            rc = ncaFileStorage.Initialize(ref baseFileSystem, in pathNca, OpenMode.Read);
            if (rc.IsFailure()) return rc;

            rc = _config.StorageOnNcaCreator.OpenNca(out Nca ncaTemp, ncaFileStorage);
            if (rc.IsFailure()) return rc;

            if (ncaId == ulong.MaxValue)
            {
                ulong ncaProgramId = ncaTemp.Header.TitleId;

                if (ncaProgramId != ulong.MaxValue && ncaId != ncaProgramId)
                {
                    return ResultFs.InvalidNcaId.Log();
                }
            }

            nca = ncaTemp;
            return Result.Success;
        }

        private Result ParseContentTypeForDirectory(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, FileSystemProxyType fsType)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);
            ReadOnlySpan<byte> dirName;

            // Get the name of the subdirectory for the filesystem type
            switch (fsType)
            {
                case FileSystemProxyType.Package:
                    fileSystem = baseFileSystem;
                    return Result.Success;

                case FileSystemProxyType.Code:
                    dirName = new[] { (byte)'/', (byte)'c', (byte)'o', (byte)'d', (byte)'e', (byte)'/' };
                    break;
                case FileSystemProxyType.Rom:
                case FileSystemProxyType.Control:
                case FileSystemProxyType.Manual:
                case FileSystemProxyType.Meta:
                case FileSystemProxyType.RegisteredUpdate:
                    dirName = new[] { (byte)'/', (byte)'d', (byte)'a', (byte)'t', (byte)'a', (byte)'/' };
                    break;
                case FileSystemProxyType.Logo:
                    dirName = new[] { (byte)'/', (byte)'l', (byte)'o', (byte)'g', (byte)'o', (byte)'/' };
                    break;

                default:
                    return ResultFs.InvalidArgument.Log();
            }

            ReferenceCountedDisposable<IFileSystem> subDirFs = null;
            try
            {
                using var directoryPath = new Path();
                Result rc = PathFunctions.SetUpFixedPath(ref directoryPath.Ref(), dirName);
                if (rc.IsFailure()) return rc;

                if (directoryPath.IsEmpty())
                    return ResultFs.InvalidArgument.Log();

                // Open the subdirectory filesystem
                rc = _config.SubDirectoryFsCreator.Create(out subDirFs, ref baseFileSystem, in directoryPath);
                if (rc.IsFailure()) return rc;

                fileSystem = Shared.Move(ref subDirFs);
                return Result.Success;
            }
            finally
            {
                subDirFs?.Dispose();
            }
        }

        private Result SetExternalKeyForRightsId(Nca nca)
        {
            var rightsId = new RightsId(nca.Header.RightsId);
            var zero = new RightsId(0, 0);

            if (Crypto.CryptoUtil.IsSameBytes(rightsId.AsBytes(), zero.AsBytes(), Unsafe.SizeOf<RightsId>()))
                return Result.Success;

            // ReSharper disable once UnusedVariable
            Result rc = _externalKeyManager.Get(rightsId, out AccessKey accessKey);
            if (rc.IsFailure()) return rc;

            // todo: Set key in nca reader

            return Result.Success;
        }

        private Result OpenStorageByContentType(out ReferenceCountedDisposable<IStorage> ncaStorage, Nca nca,
            out NcaFormatType fsType, FileSystemProxyType fsProxyType, bool isGameCard, bool canMountSystemDataPrivate)
        {
            UnsafeHelpers.SkipParamInit(out ncaStorage, out fsType);

            NcaContentType contentType = nca.Header.ContentType;

            switch (fsProxyType)
            {
                case FileSystemProxyType.Code:
                case FileSystemProxyType.Rom:
                case FileSystemProxyType.Logo:
                case FileSystemProxyType.RegisteredUpdate:
                    if (contentType != NcaContentType.Program)
                        return ResultFs.PreconditionViolation.Log();

                    break;

                case FileSystemProxyType.Control:
                    if (contentType != NcaContentType.Control)
                        return ResultFs.PreconditionViolation.Log();

                    break;
                case FileSystemProxyType.Manual:
                    if (contentType != NcaContentType.Manual)
                        return ResultFs.PreconditionViolation.Log();

                    break;
                case FileSystemProxyType.Meta:
                    if (contentType != NcaContentType.Meta)
                        return ResultFs.PreconditionViolation.Log();

                    break;
                case FileSystemProxyType.Data:
                    if (contentType != NcaContentType.Data && contentType != NcaContentType.PublicData)
                        return ResultFs.PreconditionViolation.Log();

                    if (contentType == NcaContentType.Data && !canMountSystemDataPrivate)
                        return ResultFs.PermissionDenied.Log();

                    break;
                default:
                    return ResultFs.InvalidArgument.Log();
            }

            if (nca.Header.DistributionType == DistributionType.GameCard && !isGameCard)
                return ResultFs.PermissionDenied.Log();

            Result rc = SetExternalKeyForRightsId(nca);
            if (rc.IsFailure()) return rc;

            rc = GetPartitionIndex(out int sectionIndex, fsProxyType);
            if (rc.IsFailure()) return rc;

            rc = _config.StorageOnNcaCreator.Create(out ncaStorage, out NcaFsHeader fsHeader, nca,
                sectionIndex, fsProxyType == FileSystemProxyType.Code);
            if (rc.IsFailure()) return rc;

            fsType = fsHeader.FormatType;
            return Result.Success;
        }

        public Result SetSdCardEncryptionSeed(in EncryptionSeed encryptionSeed)
        {
            _encryptionSeed = encryptionSeed;

            return Result.Success;
        }

        public Result ResolveRomReferenceProgramId(out ProgramId targetProgramId, ProgramId programId,
            byte programIndex)
        {
            UnsafeHelpers.SkipParamInit(out targetProgramId);

            ProgramId mainProgramId = _config.ProgramRegistryService.GetProgramIdByIndex(programId, programIndex);
            if (mainProgramId == ProgramId.InvalidId)
                return ResultFs.ProgramIndexNotFound.Log();

            targetProgramId = mainProgramId;
            return Result.Success;
        }

        public Result ResolveProgramPath(out bool isDirectory, ref Path path, ProgramId programId, StorageId storageId)
        {
            Result rc = _locationResolverSet.ResolveProgramPath(out isDirectory, ref path, programId, storageId);
            if (rc.IsSuccess())
                return Result.Success;

            isDirectory = false;

            rc = _locationResolverSet.ResolveDataPath(ref path, new DataId(programId.Value), storageId);
            if (rc.IsSuccess())
                return Result.Success;

            return ResultFs.TargetNotFound.Log();
        }

        public Result ResolveRomPath(out bool isDirectory, ref Path path, ulong id, StorageId storageId)
        {
            return _locationResolverSet.ResolveRomPath(out isDirectory, ref path, new ProgramId(id), storageId);
        }

        public Result ResolveApplicationHtmlDocumentPath(out bool isDirectory, ref Path path,
            Ncm.ApplicationId applicationId, StorageId storageId)
        {
            return _locationResolverSet.ResolveApplicationHtmlDocumentPath(out isDirectory, ref path, applicationId,
                storageId);
        }

        public Result ResolveRegisteredHtmlDocumentPath(ref Path path, ulong id)
        {
            return _locationResolverSet.ResolveRegisteredHtmlDocumentPath(ref path, id);
        }

        internal StorageType GetStorageFlag(ulong programId)
        {
            if (programId >= _config.SpeedEmulationRange.ProgramIdMin &&
                programId <= _config.SpeedEmulationRange.ProgramIdMax)
                return StorageType.Bis;
            else
                return StorageType.All;
        }

        public Result HandleResolubleAccessFailure(out bool wasDeferred, Result resultForNoFailureDetected,
            ulong processId)
        {
            throw new NotImplementedException();
        }

        public void IncrementRomFsRemountForDataCorruptionCount()
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _romfsCountMutex);

            _romFsRemountForDataCorruptionCount++;
        }

        public void IncrementRomFsUnrecoverableDataCorruptionByRemountCount()
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _romfsCountMutex);

            _romfsUnrecoverableDataCorruptionByRemountCount++;
        }

        public void IncrementRomFsRecoveredByInvalidateCacheCount()
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _romfsCountMutex);

            _romFsRecoveredByInvalidateCacheCount++;
        }

        public void GetAndClearRomFsErrorInfo(out int recoveredByRemountCount, out int unrecoverableCount,
            out int recoveredByCacheInvalidationCount)
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _romfsCountMutex);

            recoveredByRemountCount = _romFsRemountForDataCorruptionCount;
            unrecoverableCount = _romfsUnrecoverableDataCorruptionByRemountCount;
            recoveredByCacheInvalidationCount = _romFsRecoveredByInvalidateCacheCount;

            _romFsRemountForDataCorruptionCount = 0;
            _romfsUnrecoverableDataCorruptionByRemountCount = 0;
            _romFsRecoveredByInvalidateCacheCount = 0;
        }

        public Result OpenHostFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem, in Path rootPath, bool openCaseSensitive)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            return _config.TargetManagerFsCreator.Create(out fileSystem, in rootPath, openCaseSensitive, false,
                Result.Success);
        }

        internal Result GetProgramInfoByProcessId(out ProgramInfo programInfo, ulong processId)
        {
            var registry = new ProgramRegistryImpl(_config.FsServer);
            return registry.GetProgramInfo(out programInfo, processId);
        }

        internal Result GetProgramInfoByProgramId(out ProgramInfo programInfo, ulong programId)
        {
            var registry = new ProgramRegistryImpl(_config.FsServer);
            return registry.GetProgramInfoByProgramId(out programInfo, programId);
        }

        private Result GetPartitionIndex(out int index, FileSystemProxyType fspType)
        {
            switch (fspType)
            {
                case FileSystemProxyType.Code:
                case FileSystemProxyType.Control:
                case FileSystemProxyType.Manual:
                case FileSystemProxyType.Meta:
                case FileSystemProxyType.Data:
                    index = 0;
                    return Result.Success;
                case FileSystemProxyType.Rom:
                case FileSystemProxyType.RegisteredUpdate:
                    index = 1;
                    return Result.Success;
                case FileSystemProxyType.Logo:
                    index = 2;
                    return Result.Success;
                default:
                    UnsafeHelpers.SkipParamInit(out index);
                    return ResultFs.InvalidArgument.Log();
            }
        }

        private static ReadOnlySpan<byte> SdCardNintendoRootDirectoryName => // Nintendo
            new[]
            {
                (byte) 'N', (byte) 'i', (byte) 'n', (byte) 't', (byte) 'e', (byte) 'n', (byte) 'd', (byte) 'o'
            };

        private static ReadOnlySpan<byte> ContentStorageDirectoryName => // Contents
            new[]
            {
                (byte) 'C', (byte) 'o', (byte) 'n', (byte) 't', (byte) 'e', (byte) 'n', (byte) 't', (byte) 's'
            };
    }

    public readonly struct InternalProgramIdRangeForSpeedEmulation
    {
        public readonly ulong ProgramIdMin;
        public readonly ulong ProgramIdMax;

        public InternalProgramIdRangeForSpeedEmulation(ulong min, ulong max)
        {
            ProgramIdMin = min;
            ProgramIdMax = max;
        }
    }
}
