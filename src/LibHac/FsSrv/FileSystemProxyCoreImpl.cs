using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;

namespace LibHac.FsSrv
{
    public class FileSystemProxyCoreImpl
    {
        private FileSystemCreatorInterfaces FsCreators { get; }
        private BaseFileSystemServiceImpl BaseFileSystemService { get; }
        private EncryptionSeed SdEncryptionSeed { get; set; }

        public FileSystemProxyCoreImpl(FileSystemCreatorInterfaces fsCreators, BaseFileSystemServiceImpl baseFsService)
        {
            FsCreators = fsCreators;
            BaseFileSystemService = baseFsService;
        }

        public Result OpenCloudBackupWorkStorageFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            CloudBackupWorkStorageId storageId)
        {
            throw new NotImplementedException();
        }

        public Result OpenCustomStorageFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem,
            CustomStorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            ReferenceCountedDisposable<IFileSystem> tempFs = null;
            ReferenceCountedDisposable<IFileSystem> encryptedFs = null;
            try
            {
                Span<byte> path = stackalloc byte[0x40];

                switch (storageId)
                {
                    case CustomStorageId.SdCard:
                    {
                        Result rc = BaseFileSystemService.OpenSdCardProxyFileSystem(out tempFs);
                        if (rc.IsFailure()) return rc;

                        U8Span customStorageDir = CustomStorage.GetCustomStorageDirectoryName(CustomStorageId.SdCard);
                        var sb = new U8StringBuilder(path);
                        sb.Append((byte)'/')
                            .Append(CommonPaths.SdCardNintendoRootDirectoryName)
                            .Append((byte)'/')
                            .Append(customStorageDir);

                        rc = Utility.WrapSubDirectory(out tempFs, ref tempFs, new U8Span(path), true);
                        if (rc.IsFailure()) return rc;

                        rc = FsCreators.EncryptedFileSystemCreator.Create(out encryptedFs, tempFs,
                            EncryptedFsKeyId.CustomStorage, SdEncryptionSeed);
                        if (rc.IsFailure()) return rc;

                        return Result.Success;
                    }
                    case CustomStorageId.System:
                    {
                        Result rc = BaseFileSystemService.OpenBisFileSystem(out tempFs, U8Span.Empty,
                            BisPartitionId.User);
                        if (rc.IsFailure()) return rc;

                        U8Span customStorageDir = CustomStorage.GetCustomStorageDirectoryName(CustomStorageId.System);
                        var sb = new U8StringBuilder(path);
                        sb.Append((byte)'/')
                            .Append(customStorageDir);

                        rc = Utility.WrapSubDirectory(out tempFs, ref tempFs, new U8Span(path), true);
                        if (rc.IsFailure()) return rc;

                        fileSystem = Shared.Move(ref tempFs);
                        return Result.Success;
                    }
                    default:
                        return ResultFs.InvalidArgument.Log();
                }
            }
            finally
            {
                tempFs?.Dispose();
                encryptedFs?.Dispose();
            }
        }

        public Result OpenHostFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem, U8Span path,
            bool openCaseSensitive)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);
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
            SdEncryptionSeed = seed;
            return Result.Success;
        }
    }
}
