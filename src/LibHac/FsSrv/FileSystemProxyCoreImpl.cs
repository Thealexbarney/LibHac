using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSystem;
using LibHac.FsSrv.Creators;
using LibHac.FsSystem.NcaUtils;
using LibHac.Spl;
using LibHac.Util;
using RightsId = LibHac.Fs.RightsId;

namespace LibHac.FsSrv
{
    public class FileSystemProxyCoreImpl
    {
        internal FileSystemProxyConfiguration Config { get; }
        private FileSystemCreators FsCreators => Config.FsCreatorInterfaces;
        internal ProgramRegistryImpl ProgramRegistry { get; }

        private ExternalKeySet ExternalKeys { get; }
        private IDeviceOperator DeviceOperator { get; }

        private byte[] SdEncryptionSeed { get; } = new byte[0x10];

        private const string NintendoDirectoryName = "Nintendo";
        private const string ContentDirectoryName = "Contents";

        private GlobalAccessLogMode LogMode { get; set; }
        public bool IsSdCardAccessible { get; set; }

        internal ISaveDataIndexerManager SaveDataIndexerManager { get; private set; }

        public FileSystemProxyCoreImpl(FileSystemProxyConfiguration config, ExternalKeySet externalKeys, IDeviceOperator deviceOperator)
        {
            Config = config;
            ProgramRegistry = new ProgramRegistryImpl(Config.ProgramRegistryServiceImpl);
            ExternalKeys = externalKeys ?? new ExternalKeySet();
            DeviceOperator = deviceOperator;
        }

        public Result OpenFileSystem(out IFileSystem fileSystem, U8Span path, FileSystemProxyType type,
            bool canMountSystemDataPrivate, ulong programId)
        {
            fileSystem = default;

            // Get a reference to the path that will be advanced as each part of the path is parsed
            U8Span currentPath = path.Slice(0, StringUtils.GetLength(path));

            // Open the root filesystem based on the path's mount name
            Result rc = OpenFileSystemFromMountName(ref currentPath, out IFileSystem baseFileSystem, out bool shouldContinue,
                out MountNameInfo mountNameInfo);
            if (rc.IsFailure()) return rc;

            // Don't continue if the rest of the path is empty
            if (!shouldContinue)
                return ResultFs.InvalidArgument.Log();

            if (type == FileSystemProxyType.Logo && mountNameInfo.IsGameCard)
            {
                rc = OpenGameCardFileSystem(out fileSystem, new GameCardHandle(mountNameInfo.GcHandle),
                    GameCardPartition.Logo);

                if (rc.IsSuccess())
                    return Result.Success;

                if (!ResultFs.PartitionNotFound.Includes(rc))
                    return rc;
            }

            rc = IsContentPathDir(ref currentPath, out bool isDirectory);
            if (rc.IsFailure()) return rc;

            if (isDirectory)
            {
                if (!mountNameInfo.IsHostFs)
                    return ResultFs.PermissionDenied.Log();

                if (type == FileSystemProxyType.Manual)
                {
                    rc = TryOpenCaseSensitiveContentDirectory(out IFileSystem manualFileSystem, baseFileSystem, currentPath);
                    if (rc.IsFailure()) return rc;

                    fileSystem = new ReadOnlyFileSystem(manualFileSystem);
                    return Result.Success;
                }

                return TryOpenContentDirectory(currentPath, out fileSystem, baseFileSystem, type, true);
            }

            rc = TryOpenNsp(ref currentPath, out IFileSystem nspFileSystem, baseFileSystem);

            if (rc.IsSuccess())
            {
                // Must be the end of the path to open Application Package FS type
                if (currentPath.Length == 0 || currentPath[0] == 0)
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
                return ResultFs.InvalidNcaMountPoint.Log();
            }

            ulong openProgramId = mountNameInfo.IsHostFs ? ulong.MaxValue : programId;

            rc = TryOpenNca(ref currentPath, out Nca nca, baseFileSystem, openProgramId);
            if (rc.IsFailure()) return rc;

            rc = OpenNcaStorage(out IStorage ncaSectionStorage, nca, out NcaFormatType fsType, type,
                mountNameInfo.IsGameCard, canMountSystemDataPrivate);
            if (rc.IsFailure()) return rc;

            switch (fsType)
            {
                case NcaFormatType.Romfs:
                    return FsCreators.RomFileSystemCreator.Create(out fileSystem, ncaSectionStorage);
                case NcaFormatType.Pfs0:
                    return FsCreators.PartitionFileSystemCreator.Create(out fileSystem, ncaSectionStorage);
                default:
                    return ResultFs.InvalidNcaFsType.Log();
            }
        }

        /// <summary>
        /// Stores info obtained by parsing a common mount name.
        /// </summary>
        private struct MountNameInfo
        {
            public bool IsGameCard;
            public int GcHandle;
            public bool IsHostFs;
            public bool CanMountNca;
        }

        private Result OpenFileSystemFromMountName(ref U8Span path, out IFileSystem fileSystem, out bool shouldContinue,
            out MountNameInfo info)
        {
            fileSystem = default;

            info = new MountNameInfo();
            shouldContinue = true;

            if (StringUtils.Compare(path, CommonMountNames.GameCardFileSystemMountName,
                    CommonMountNames.GameCardFileSystemMountName.Length) == 0)
            {
                path = path.Slice(CommonMountNames.GameCardFileSystemMountName.Length);

                if (StringUtils.GetLength(path.Value, 9) < 9)
                    return ResultFs.InvalidPath.Log();

                GameCardPartition partition;
                switch ((char)path[0])
                {
                    case CommonMountNames.GameCardFileSystemMountNameUpdateSuffix:
                        partition = GameCardPartition.Update;
                        break;
                    case CommonMountNames.GameCardFileSystemMountNameNormalSuffix:
                        partition = GameCardPartition.Normal;
                        break;
                    case CommonMountNames.GameCardFileSystemMountNameSecureSuffix:
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

                Result rc = OpenGameCardFileSystem(out fileSystem, new GameCardHandle(handle), partition);
                if (rc.IsFailure()) return rc;

                info.GcHandle = handle;
                info.IsGameCard = true;
                info.CanMountNca = true;
            }

            else if (StringUtils.Compare(path, CommonMountNames.ContentStorageSystemMountName,
                         CommonMountNames.ContentStorageSystemMountName.Length) == 0)
            {
                path = path.Slice(CommonMountNames.ContentStorageSystemMountName.Length);

                Result rc = OpenContentStorageFileSystem(out fileSystem, ContentStorageId.System);
                if (rc.IsFailure()) return rc;

                info.CanMountNca = true;
            }

            else if (StringUtils.Compare(path, CommonMountNames.ContentStorageUserMountName,
                         CommonMountNames.ContentStorageUserMountName.Length) == 0)
            {
                path = path.Slice(CommonMountNames.ContentStorageUserMountName.Length);

                Result rc = OpenContentStorageFileSystem(out fileSystem, ContentStorageId.User);
                if (rc.IsFailure()) return rc;

                info.CanMountNca = true;
            }

            else if (StringUtils.Compare(path, CommonMountNames.ContentStorageSdCardMountName,
                         CommonMountNames.ContentStorageSdCardMountName.Length) == 0)
            {
                path = path.Slice(CommonMountNames.ContentStorageSdCardMountName.Length);

                Result rc = OpenContentStorageFileSystem(out fileSystem, ContentStorageId.SdCard);
                if (rc.IsFailure()) return rc;

                info.CanMountNca = true;
            }

            else if (StringUtils.Compare(path, CommonMountNames.BisCalibrationFilePartitionMountName,
                         CommonMountNames.BisCalibrationFilePartitionMountName.Length) == 0)
            {
                path = path.Slice(CommonMountNames.BisCalibrationFilePartitionMountName.Length);

                Result rc = OpenBisFileSystem(out fileSystem, string.Empty, BisPartitionId.CalibrationFile);
                if (rc.IsFailure()) return rc;
            }

            else if (StringUtils.Compare(path, CommonMountNames.BisSafeModePartitionMountName,
                         CommonMountNames.BisSafeModePartitionMountName.Length) == 0)
            {
                path = path.Slice(CommonMountNames.BisSafeModePartitionMountName.Length);

                Result rc = OpenBisFileSystem(out fileSystem, string.Empty, BisPartitionId.SafeMode);
                if (rc.IsFailure()) return rc;
            }

            else if (StringUtils.Compare(path, CommonMountNames.BisUserPartitionMountName,
                         CommonMountNames.BisUserPartitionMountName.Length) == 0)
            {
                path = path.Slice(CommonMountNames.BisUserPartitionMountName.Length);

                Result rc = OpenBisFileSystem(out fileSystem, string.Empty, BisPartitionId.User);
                if (rc.IsFailure()) return rc;
            }

            else if (StringUtils.Compare(path, CommonMountNames.BisSystemPartitionMountName,
                         CommonMountNames.BisSystemPartitionMountName.Length) == 0)
            {
                path = path.Slice(CommonMountNames.BisSystemPartitionMountName.Length);

                Result rc = OpenBisFileSystem(out fileSystem, string.Empty, BisPartitionId.System);
                if (rc.IsFailure()) return rc;
            }

            else if (StringUtils.Compare(path, CommonMountNames.SdCardFileSystemMountName,
                         CommonMountNames.SdCardFileSystemMountName.Length) == 0)
            {
                path = path.Slice(CommonMountNames.SdCardFileSystemMountName.Length);

                Result rc = OpenSdCardFileSystem(out fileSystem);
                if (rc.IsFailure()) return rc;
            }

            else if (StringUtils.Compare(path, CommonMountNames.HostRootFileSystemMountName,
                         CommonMountNames.HostRootFileSystemMountName.Length) == 0)
            {
                path = path.Slice(CommonMountNames.HostRootFileSystemMountName.Length);

                info.IsHostFs = true;
                info.CanMountNca = true;

                Result rc = OpenHostFileSystem(out fileSystem, U8Span.Empty, openCaseSensitive: false);
                if (rc.IsFailure()) return rc;
            }

            else if (StringUtils.Compare(path, CommonMountNames.RegisteredUpdatePartitionMountName,
                         CommonMountNames.RegisteredUpdatePartitionMountName.Length) == 0)
            {
                path = path.Slice(CommonMountNames.RegisteredUpdatePartitionMountName.Length);

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

        private Result IsContentPathDir(ref U8Span path, out bool isDirectory)
        {
            isDirectory = default;

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

        private Result TryOpenContentDirectory(U8Span path, out IFileSystem contentFileSystem,
            IFileSystem baseFileSystem, FileSystemProxyType fsType, bool preserveUnc)
        {
            contentFileSystem = default;

            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            Result rc = FsCreators.SubDirectoryFileSystemCreator.Create(out IFileSystem subDirFs,
                baseFileSystem, path, preserveUnc);
            if (rc.IsFailure()) return rc;

            return OpenSubDirectoryForFsType(out contentFileSystem, subDirFs, fsType);
        }

        private Result TryOpenCaseSensitiveContentDirectory(out IFileSystem contentFileSystem,
            IFileSystem baseFileSystem, U8Span path)
        {
            contentFileSystem = default;
            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            var sb = new U8StringBuilder(fullPath.Str);
            sb.Append(path)
            .Append(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a', (byte)'/' });

            if (sb.Overflowed)
                return ResultFs.TooLongPath.Log();

            Result rc = FsCreators.TargetManagerFileSystemCreator.GetCaseSensitivePath(out bool success, fullPath.Str);
            if (rc.IsFailure()) return rc;

            // Reopen the host filesystem as case sensitive
            if (success)
            {
                baseFileSystem.Dispose();

                rc = OpenHostFileSystem(out baseFileSystem, U8Span.Empty, openCaseSensitive: true);
                if (rc.IsFailure()) return rc;
            }

            return FsCreators.SubDirectoryFileSystemCreator.Create(out contentFileSystem, baseFileSystem, fullPath);
        }

        private Result OpenSubDirectoryForFsType(out IFileSystem fileSystem, IFileSystem baseFileSystem,
            FileSystemProxyType fsType)
        {
            fileSystem = default;
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

            // Open the subdirectory filesystem
            Result rc = FsCreators.SubDirectoryFileSystemCreator.Create(out IFileSystem subDirFs, baseFileSystem,
                new U8Span(dirName));
            if (rc.IsFailure()) return rc;

            if (fsType == FileSystemProxyType.Code)
            {
                rc = FsCreators.StorageOnNcaCreator.VerifyAcidSignature(subDirFs, null);
                if (rc.IsFailure()) return rc;
            }

            fileSystem = subDirFs;
            return Result.Success;
        }

        private Result TryOpenNsp(ref U8Span path, out IFileSystem outFileSystem, IFileSystem baseFileSystem)
        {
            outFileSystem = default;

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

            if (nspPathLen > FsPath.MaxLength + 1)
                return ResultFs.TooLongPath.Log();

            Result rc = FsPath.FromSpan(out FsPath nspPath, path.Slice(0, nspPathLen));
            if (rc.IsFailure()) return rc;

            rc = FileStorageBasedFileSystem.CreateNew(out FileStorageBasedFileSystem nspFileStorage, baseFileSystem,
                new U8Span(nspPath.Str), OpenMode.Read);
            if (rc.IsFailure()) return rc;

            rc = FsCreators.PartitionFileSystemCreator.Create(out outFileSystem, nspFileStorage);

            if (rc.IsSuccess())
            {
                path = path.Slice(nspPathLen);
            }

            return rc;
        }

        private Result TryOpenNca(ref U8Span path, out Nca nca, IFileSystem baseFileSystem, ulong ncaId)
        {
            nca = default;

            Result rc = FileStorageBasedFileSystem.CreateNew(out FileStorageBasedFileSystem ncaFileStorage,
                baseFileSystem, path, OpenMode.Read);
            if (rc.IsFailure()) return rc;

            rc = FsCreators.StorageOnNcaCreator.OpenNca(out Nca ncaTemp, ncaFileStorage);
            if (rc.IsFailure()) return rc;

            if (ncaId == ulong.MaxValue)
            {
                ulong ncaProgramId = ncaTemp.Header.TitleId;

                if (ncaProgramId != ulong.MaxValue && ncaId != ncaProgramId)
                {
                    return ResultFs.InvalidNcaProgramId.Log();
                }
            }

            nca = ncaTemp;
            return Result.Success;
        }

        private Result OpenNcaStorage(out IStorage ncaStorage, Nca nca, out NcaFormatType fsType,
            FileSystemProxyType fsProxyType, bool isGameCard, bool canMountSystemDataPrivate)
        {
            ncaStorage = default;
            fsType = default;

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

            Result rc = SetNcaExternalKey(nca);
            if (rc.IsFailure()) return rc;

            rc = GetNcaSectionIndex(out int sectionIndex, fsProxyType);
            if (rc.IsFailure()) return rc;

            rc = FsCreators.StorageOnNcaCreator.Create(out ncaStorage, out NcaFsHeader fsHeader, nca,
                sectionIndex, fsProxyType == FileSystemProxyType.Code);
            if (rc.IsFailure()) return rc;

            fsType = fsHeader.FormatType;
            return Result.Success;
        }

        private Result SetNcaExternalKey(Nca nca)
        {
            var rightsId = new RightsId(nca.Header.RightsId);
            var zero = new RightsId(0, 0);

            if (Crypto.CryptoUtil.IsSameBytes(rightsId.AsBytes(), zero.AsBytes(), Unsafe.SizeOf<RightsId>()))
                return Result.Success;

            // ReSharper disable once UnusedVariable
            Result rc = ExternalKeys.Get(rightsId, out AccessKey accessKey);
            if (rc.IsFailure()) return rc;

            // todo: Set key in nca reader

            return Result.Success;
        }

        private Result GetNcaSectionIndex(out int index, FileSystemProxyType fspType)
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
                    index = default;
                    return ResultFs.InvalidArgument.Log();
            }
        }

        public Result OpenBisFileSystem(out IFileSystem fileSystem, string rootPath, BisPartitionId partitionId)
        {
            return FsCreators.BuiltInStorageFileSystemCreator.Create(out fileSystem, rootPath, partitionId);
        }

        public Result OpenSdCardFileSystem(out IFileSystem fileSystem)
        {
            return FsCreators.SdCardFileSystemCreator.Create(out fileSystem, false);
        }

        public Result OpenGameCardStorage(out IStorage storage, GameCardHandle handle, GameCardPartitionRaw partitionId)
        {
            switch (partitionId)
            {
                case GameCardPartitionRaw.NormalReadOnly:
                    return FsCreators.GameCardStorageCreator.CreateNormal(handle, out storage);
                case GameCardPartitionRaw.SecureReadOnly:
                    return FsCreators.GameCardStorageCreator.CreateSecure(handle, out storage);
                case GameCardPartitionRaw.RootWriteOnly:
                    return FsCreators.GameCardStorageCreator.CreateWritable(handle, out storage);
                default:
                    throw new ArgumentOutOfRangeException(nameof(partitionId), partitionId, null);
            }
        }

        public Result OpenDeviceOperator(out IDeviceOperator deviceOperator)
        {
            deviceOperator = DeviceOperator;
            return Result.Success;
        }

        public Result OpenContentStorageFileSystem(out IFileSystem fileSystem, ContentStorageId storageId)
        {
            fileSystem = default;

            U8String contentDirPath = default;
            IFileSystem baseFileSystem = default;
            bool isEncrypted = false;
            Result rc;

            switch (storageId)
            {
                case ContentStorageId.System:
                    rc = OpenBisFileSystem(out baseFileSystem, string.Empty, BisPartitionId.System);
                    contentDirPath = $"/{ContentDirectoryName}".ToU8String();
                    break;
                case ContentStorageId.User:
                    rc = OpenBisFileSystem(out baseFileSystem, string.Empty, BisPartitionId.User);
                    contentDirPath = $"/{ContentDirectoryName}".ToU8String();
                    break;
                case ContentStorageId.SdCard:
                    rc = OpenSdCardFileSystem(out baseFileSystem);
                    contentDirPath = $"/{NintendoDirectoryName}/{ContentDirectoryName}".ToU8String();
                    isEncrypted = true;
                    break;
                default:
                    rc = ResultFs.InvalidArgument.Log();
                    break;
            }

            if (rc.IsFailure()) return rc;

            rc = baseFileSystem.EnsureDirectoryExists(contentDirPath.ToString());
            if (rc.IsFailure()) return rc;

            rc = FsCreators.SubDirectoryFileSystemCreator.Create(out IFileSystem subDirFileSystem,
                baseFileSystem, contentDirPath);
            if (rc.IsFailure()) return rc;

            if (!isEncrypted)
            {
                fileSystem = subDirFileSystem;
                return Result.Success;
            }

            return FsCreators.EncryptedFileSystemCreator.Create(out fileSystem, subDirFileSystem,
                EncryptedFsKeyId.Content, SdEncryptionSeed);
        }

        public Result OpenCustomStorageFileSystem(out IFileSystem fileSystem, CustomStorageId storageId)
        {
            fileSystem = default;

            switch (storageId)
            {
                case CustomStorageId.SdCard:
                {
                    Result rc = FsCreators.SdCardFileSystemCreator.Create(out IFileSystem sdFs, false);
                    if (rc.IsFailure()) return rc;

                    string customStorageDir = CustomStorage.GetCustomStorageDirectoryName(CustomStorageId.SdCard);
                    string subDirName = $"/{NintendoDirectoryName}/{customStorageDir}";

                    rc = Util.CreateSubFileSystem(out IFileSystem subFs, sdFs, subDirName, true);
                    if (rc.IsFailure()) return rc;

                    rc = FsCreators.EncryptedFileSystemCreator.Create(out IFileSystem encryptedFs, subFs,
                        EncryptedFsKeyId.CustomStorage, SdEncryptionSeed);
                    if (rc.IsFailure()) return rc;

                    fileSystem = encryptedFs;
                    return Result.Success;
                }
                case CustomStorageId.System:
                {
                    Result rc = FsCreators.BuiltInStorageFileSystemCreator.Create(out IFileSystem userFs, string.Empty,
                        BisPartitionId.User);
                    if (rc.IsFailure()) return rc;

                    string customStorageDir = CustomStorage.GetCustomStorageDirectoryName(CustomStorageId.System);
                    string subDirName = $"/{customStorageDir}";

                    rc = Util.CreateSubFileSystem(out IFileSystem subFs, userFs, subDirName, true);
                    if (rc.IsFailure()) return rc;

                    fileSystem = subFs;
                    return Result.Success;
                }
                default:
                    return ResultFs.InvalidArgument.Log();
            }
        }

        public Result OpenGameCardFileSystem(out IFileSystem fileSystem, GameCardHandle handle,
            GameCardPartition partitionId)
        {
            Result rc;
            int tries = 0;

            do
            {
                rc = FsCreators.GameCardFileSystemCreator.Create(out fileSystem, handle, partitionId);

                if (!ResultFs.DataCorrupted.Includes(rc))
                    break;

                tries++;
            } while (tries < 2);

            return rc;
        }

        public Result OpenHostFileSystem(out IFileSystem fileSystem, U8Span path, bool openCaseSensitive)
        {
            fileSystem = default;
            Result rc;

            if (!path.IsEmpty())
            {
                rc = Util.VerifyHostPath(path);
                if (rc.IsFailure()) return rc;
            }

            rc = FsCreators.TargetManagerFileSystemCreator.Create(out IFileSystem hostFs, openCaseSensitive);
            if (rc.IsFailure()) return rc;

            if (path.IsEmpty())
            {
                ReadOnlySpan<byte> rootHostPath = new[] { (byte)'C', (byte)':', (byte)'/' };
                rc = hostFs.GetEntryType(out _, new U8Span(rootHostPath));

                // Nintendo ignores all results other than this one
                if (ResultFs.TargetNotFound.Includes(rc))
                    return rc;

                fileSystem = hostFs;
                return Result.Success;
            }

            rc = FsCreators.SubDirectoryFileSystemCreator.Create(out IFileSystem subDirFs, hostFs, path, preserveUnc: true);
            if (rc.IsFailure()) return rc;

            fileSystem = subDirFs;
            return Result.Success;
        }

        public Result RegisterExternalKey(ref RightsId rightsId, ref AccessKey externalKey)
        {
            return ExternalKeys.Add(rightsId, externalKey);
        }

        public Result UnregisterExternalKey(ref RightsId rightsId)
        {
            ExternalKeys.Remove(rightsId);

            return Result.Success;
        }

        public Result UnregisterAllExternalKey()
        {
            ExternalKeys.Clear();

            return Result.Success;
        }

        public Result SetSdCardEncryptionSeed(ref EncryptionSeed seed)
        {
            seed.Value.CopyTo(SdEncryptionSeed);
            // todo: FsCreators.SaveDataFileSystemCreator.SetSdCardEncryptionSeed(seed);

            SaveDataIndexerManager.InvalidateSdCardIndexer(SaveDataSpaceId.SdSystem);
            SaveDataIndexerManager.InvalidateSdCardIndexer(SaveDataSpaceId.SdCache);

            return Result.Success;
        }

        public bool AllowDirectorySaveData(SaveDataSpaceId spaceId, string saveDataRootPath)
        {
            return spaceId == SaveDataSpaceId.User && !string.IsNullOrWhiteSpace(saveDataRootPath);
        }

        public Result DoesSaveDataExist(out bool exists, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            exists = false;

            Result rc = OpenSaveDataDirectory(out IFileSystem fileSystem, spaceId, string.Empty, true);
            if (rc.IsFailure()) return rc;

            string saveDataPath = $"/{saveDataId:x16}";

            rc = fileSystem.GetEntryType(out _, saveDataPath.ToU8Span());

            if (rc.IsFailure())
            {
                if (ResultFs.PathNotFound.Includes(rc))
                {
                    return Result.Success;
                }

                return rc;
            }

            exists = true;
            return Result.Success;
        }

        public Result OpenSaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ulong saveDataId,
            string saveDataRootPath, bool openReadOnly, SaveDataType type, bool cacheExtraData)
        {
            fileSystem = default;

            Result rc = OpenSaveDataDirectory(out IFileSystem saveDirFs, spaceId, saveDataRootPath, true);
            if (rc.IsFailure()) return rc;

            // ReSharper disable once RedundantAssignment
            bool allowDirectorySaveData = AllowDirectorySaveData(spaceId, saveDataRootPath);
            bool useDeviceUniqueMac = Util.UseDeviceUniqueSaveMac(spaceId);

            // Always allow directory savedata because we don't support transaction with file savedata yet
            allowDirectorySaveData = true;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (allowDirectorySaveData)
            {
                rc = saveDirFs.EnsureDirectoryExists(GetSaveDataIdPath(saveDataId));
                if (rc.IsFailure()) return rc;
            }

            // Missing save FS cache lookup

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            rc = FsCreators.SaveDataFileSystemCreator.Create(out IFileSystem saveFs, out _, saveDirFs, saveDataId,
                allowDirectorySaveData, useDeviceUniqueMac, type, null);

            if (rc.IsFailure()) return rc;

            if (cacheExtraData)
            {
                // todo: Missing extra data caching
            }

            fileSystem = openReadOnly ? new ReadOnlyFileSystem(saveFs) : saveFs;

            return Result.Success;
        }

        public Result OpenSaveDataDirectory(out IFileSystem fileSystem, SaveDataSpaceId spaceId, string saveDataRootPath, bool openOnHostFs)
        {
            if (openOnHostFs && AllowDirectorySaveData(spaceId, saveDataRootPath))
            {
                Result rc = FsCreators.TargetManagerFileSystemCreator.Create(out IFileSystem hostFs, false);

                if (rc.IsFailure())
                {
                    fileSystem = default;
                    return rc;
                }

                return Util.CreateSubFileSystem(out fileSystem, hostFs, saveDataRootPath, true);
            }

            string dirName = spaceId == SaveDataSpaceId.Temporary ? "/temp" : "/save";

            return OpenSaveDataDirectoryImpl(out fileSystem, spaceId, dirName, true);
        }

        public Result OpenSaveDataDirectoryImpl(out IFileSystem fileSystem, SaveDataSpaceId spaceId, string saveDirName, bool createIfMissing)
        {
            fileSystem = default;
            Result rc;

            switch (spaceId)
            {
                case SaveDataSpaceId.System:
                    rc = OpenBisFileSystem(out IFileSystem sysFs, string.Empty, BisPartitionId.System);
                    if (rc.IsFailure()) return rc;

                    return Util.CreateSubFileSystem(out fileSystem, sysFs, saveDirName, createIfMissing);

                case SaveDataSpaceId.User:
                case SaveDataSpaceId.Temporary:
                    rc = OpenBisFileSystem(out IFileSystem userFs, string.Empty, BisPartitionId.User);
                    if (rc.IsFailure()) return rc;

                    return Util.CreateSubFileSystem(out fileSystem, userFs, saveDirName, createIfMissing);

                case SaveDataSpaceId.SdSystem:
                case SaveDataSpaceId.SdCache:
                    rc = OpenSdCardFileSystem(out IFileSystem sdFs);
                    if (rc.IsFailure()) return rc;

                    string sdSaveDirPath = $"/{NintendoDirectoryName}{saveDirName}";

                    rc = Util.CreateSubFileSystem(out IFileSystem sdSubFs, sdFs, sdSaveDirPath, createIfMissing);
                    if (rc.IsFailure()) return rc;

                    return FsCreators.EncryptedFileSystemCreator.Create(out fileSystem, sdSubFs,
                        EncryptedFsKeyId.Save, SdEncryptionSeed);

                case SaveDataSpaceId.ProperSystem:
                    rc = OpenBisFileSystem(out IFileSystem sysProperFs, string.Empty, BisPartitionId.SystemProperPartition);
                    if (rc.IsFailure()) return rc;

                    return Util.CreateSubFileSystem(out fileSystem, sysProperFs, saveDirName, createIfMissing);

                case SaveDataSpaceId.SafeMode:
                    rc = OpenBisFileSystem(out IFileSystem safeFs, string.Empty, BisPartitionId.SafeMode);
                    if (rc.IsFailure()) return rc;

                    return Util.CreateSubFileSystem(out fileSystem, safeFs, saveDirName, createIfMissing);

                default:
                    return ResultFs.InvalidArgument.Log();
            }
        }

        public Result OpenSaveDataMetaFile(out IFile file, ulong saveDataId, SaveDataSpaceId spaceId, SaveDataMetaType type)
        {
            file = default;

            string metaDirPath = $"/saveMeta/{saveDataId:x16}";

            Result rc = OpenSaveDataDirectoryImpl(out IFileSystem tmpMetaDirFs, spaceId, metaDirPath, true);
            using IFileSystem metaDirFs = tmpMetaDirFs;
            if (rc.IsFailure()) return rc;

            string metaFilePath = $"/{(int)type:x8}.meta";

            return metaDirFs.OpenFile(out file, metaFilePath.ToU8Span(), OpenMode.ReadWrite);
        }

        public Result DeleteSaveDataMetaFiles(ulong saveDataId, SaveDataSpaceId spaceId)
        {
            Result rc = OpenSaveDataDirectoryImpl(out IFileSystem metaDirFs, spaceId, "/saveMeta", false);

            using (metaDirFs)
            {
                if (rc.IsFailure()) return rc;

                rc = metaDirFs.DeleteDirectoryRecursively($"/{saveDataId:x16}".ToU8Span());

                if (rc.IsFailure() && !ResultFs.PathNotFound.Includes(rc))
                    return rc;

                return Result.Success;
            }
        }

        public Result CreateSaveDataMetaFile(ulong saveDataId, SaveDataSpaceId spaceId, SaveDataMetaType type, long size)
        {
            string metaDirPath = $"/saveMeta/{saveDataId:x16}";

            Result rc = OpenSaveDataDirectoryImpl(out IFileSystem tmpMetaDirFs, spaceId, metaDirPath, true);
            using IFileSystem metaDirFs = tmpMetaDirFs;
            if (rc.IsFailure()) return rc;

            string metaFilePath = $"/{(int)type:x8}.meta";

            if (size < 0) return ResultFs.OutOfRange.Log();

            return metaDirFs.CreateFile(metaFilePath.ToU8Span(), size, CreateFileOptions.None);
        }

        public Result CreateSaveDataFileSystem(ulong saveDataId, ref SaveDataAttribute attribute,
            ref SaveDataCreationInfo creationInfo, U8Span rootPath, OptionalHashSalt hashSalt, bool something)
        {
            // Use directory save data for now

            Result rc = OpenSaveDataDirectory(out IFileSystem fileSystem, creationInfo.SpaceId, string.Empty, false);
            if (rc.IsFailure()) return rc;

            return fileSystem.EnsureDirectoryExists(GetSaveDataIdPath(saveDataId));
        }

        public Result DeleteSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, bool doSecureDelete)
        {
            Result rc = OpenSaveDataDirectory(out IFileSystem fileSystem, spaceId, string.Empty, false);

            using (fileSystem)
            {
                if (rc.IsFailure()) return rc;

                var saveDataPath = GetSaveDataIdPath(saveDataId).ToU8Span();

                rc = fileSystem.GetEntryType(out DirectoryEntryType entryType, saveDataPath);
                if (rc.IsFailure()) return rc;

                if (entryType == DirectoryEntryType.Directory)
                {
                    rc = fileSystem.DeleteDirectoryRecursively(saveDataPath);
                }
                else
                {
                    if (doSecureDelete)
                    {
                        // Overwrite file with garbage before deleting
                        throw new NotImplementedException();
                    }

                    rc = fileSystem.DeleteFile(saveDataPath);
                }

                return rc;
            }
        }

        public Result SetGlobalAccessLogMode(GlobalAccessLogMode mode)
        {
            LogMode = mode;
            return Result.Success;
        }

        public Result GetGlobalAccessLogMode(out GlobalAccessLogMode mode)
        {
            mode = LogMode;
            return Result.Success;
        }

        internal void SetSaveDataIndexerManager(ISaveDataIndexerManager manager)
        {
            SaveDataIndexerManager = manager;
        }

        internal Result OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, out bool neededInit, SaveDataSpaceId spaceId)
        {
            return SaveDataIndexerManager.OpenAccessor(out accessor, out neededInit, spaceId);
        }

        private string GetSaveDataIdPath(ulong saveDataId)
        {
            return $"/{saveDataId:x16}";
        }
    }
}
