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

        private byte[] SdEncryptionSeed { get; } = new byte[0x10];

        private const string NintendoDirectoryName = "Nintendo";

        public FileSystemProxyCoreImpl(FileSystemProxyConfiguration config)
        {
            Config = config;
            ProgramRegistry = new ProgramRegistryImpl(Config.ProgramRegistryService);
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
            return Result.Success;
        }
    }
}
