using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Creators;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;

namespace LibHac.FsSrv
{
    public class FileSystemProxyCoreImpl
    {
        internal FileSystemProxyConfiguration Config { get; }
        private FileSystemCreators FsCreators => Config.FsCreatorInterfaces;
        internal ProgramRegistryImpl ProgramRegistry { get; }

        private ReferenceCountedDisposable<IDeviceOperator> DeviceOperator { get; }

        private byte[] SdEncryptionSeed { get; } = new byte[0x10];

        private const string NintendoDirectoryName = "Nintendo";

        private GlobalAccessLogMode LogMode { get; set; }

        internal ISaveDataIndexerManager SaveDataIndexerManager { get; private set; }

        public FileSystemProxyCoreImpl(FileSystemProxyConfiguration config, IDeviceOperator deviceOperator)
        {
            Config = config;
            ProgramRegistry = new ProgramRegistryImpl(Config.ProgramRegistryService);
            DeviceOperator = new ReferenceCountedDisposable<IDeviceOperator>(deviceOperator);
        }

        public Result OpenGameCardStorage(out ReferenceCountedDisposable<IStorageSf> storage, GameCardHandle handle,
            GameCardPartitionRaw partitionId)
        {
            storage = default;

            Result rc;
            IStorage gcStorage = null;
            ReferenceCountedDisposable<IStorage> sharedGcStorage = null;
            try
            {
                switch (partitionId)
                {
                    case GameCardPartitionRaw.NormalReadOnly:
                        rc = FsCreators.GameCardStorageCreator.CreateNormal(handle, out gcStorage);
                        break;
                    case GameCardPartitionRaw.SecureReadOnly:
                        rc = FsCreators.GameCardStorageCreator.CreateSecure(handle, out gcStorage);
                        break;
                    case GameCardPartitionRaw.RootWriteOnly:
                        rc = FsCreators.GameCardStorageCreator.CreateWritable(handle, out gcStorage);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(partitionId), partitionId, null);
                }

                if (rc.IsFailure()) return rc;

                sharedGcStorage = new ReferenceCountedDisposable<IStorage>(gcStorage);
                gcStorage = null;

                storage = StorageInterfaceAdapter.CreateShared(ref sharedGcStorage);
                return Result.Success;
            }
            finally
            {
                gcStorage?.Dispose();
                sharedGcStorage?.Dispose();
            }
        }

        public Result OpenDeviceOperator(out ReferenceCountedDisposable<IDeviceOperator> deviceOperator)
        {
            deviceOperator = DeviceOperator.AddReference();
            return Result.Success;
        }

        public Result OpenCustomStorageFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            CustomStorageId storageId)
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

                    fileSystem = new ReferenceCountedDisposable<IFileSystem>(encryptedFs);
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

                    // Todo: Get shared object from earlier functions
                    fileSystem = new ReferenceCountedDisposable<IFileSystem>(subFs);
                    return Result.Success;
                }
                default:
                    return ResultFs.InvalidArgument.Log();
            }
        }

        public Result OpenHostFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem, U8Span path,
            bool openCaseSensitive)
        {
            fileSystem = default;
            Result rc;

            if (!path.IsEmpty())
            {
                rc = Util.VerifyHostPath(path);
                if (rc.IsFailure()) return rc;
            }

            // Todo: Return shared fs from Create
            rc = FsCreators.TargetManagerFileSystemCreator.Create(out IFileSystem hostFs, openCaseSensitive);
            if (rc.IsFailure()) return rc;

            ReferenceCountedDisposable<IFileSystem> sharedHostFs = null;
            ReferenceCountedDisposable<IFileSystem> subDirFs = null;

            try
            {
                sharedHostFs = new ReferenceCountedDisposable<IFileSystem>(hostFs);

                if (path.IsEmpty())
                {
                    ReadOnlySpan<byte> rootHostPath = new[] { (byte)'C', (byte)':', (byte)'/' };
                    rc = sharedHostFs.Target.GetEntryType(out _, new U8Span(rootHostPath));

                    // Nintendo ignores all results other than this one
                    if (ResultFs.TargetNotFound.Includes(rc))
                        return rc;

                    Shared.Move(out fileSystem, ref sharedHostFs);
                    return Result.Success;
                }

                rc = FsCreators.SubDirectoryFileSystemCreator.Create(out subDirFs, ref sharedHostFs, path,
                    preserveUnc: true);
                if (rc.IsFailure()) return rc;

                fileSystem = subDirFs;
                return Result.Success;
            }
            finally
            {
                sharedHostFs?.Dispose();
                subDirFs?.Dispose();
            }
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
