using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Creators;

namespace LibHac.FsSrv
{
    public class FileSystemProxyCoreImpl
    {
        internal FileSystemProxyConfiguration Config { get; }
        private FileSystemCreators FsCreators => Config.FsCreatorInterfaces;
        internal ProgramRegistryImpl ProgramRegistry { get; }

        private IDeviceOperator DeviceOperator { get; }

        private byte[] SdEncryptionSeed { get; } = new byte[0x10];

        private const string NintendoDirectoryName = "Nintendo";

        private GlobalAccessLogMode LogMode { get; set; }

        internal ISaveDataIndexerManager SaveDataIndexerManager { get; private set; }

        public FileSystemProxyCoreImpl(FileSystemProxyConfiguration config, IDeviceOperator deviceOperator)
        {
            Config = config;
            ProgramRegistry = new ProgramRegistryImpl(Config.ProgramRegistryService);
            DeviceOperator = deviceOperator;
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

        public Result SetSdCardEncryptionSeed(in EncryptionSeed seed)
        {
            seed.Value.CopyTo(SdEncryptionSeed);
            // todo: FsCreators.SaveDataFileSystemCreator.SetSdCardEncryptionSeed(seed);

            SaveDataIndexerManager.InvalidateIndexer(SaveDataSpaceId.SdSystem);
            SaveDataIndexerManager.InvalidateIndexer(SaveDataSpaceId.SdCache);

            return Result.Success;
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
    }
}
