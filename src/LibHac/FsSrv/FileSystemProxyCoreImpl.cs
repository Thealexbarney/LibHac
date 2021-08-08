using System;
using System.Runtime.InteropServices;
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

        public Result OpenCustomStorageFileSystem(out ReferenceCountedDisposable<IFileSystem> outFileSystem,
            CustomStorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out outFileSystem);

            ReferenceCountedDisposable<IFileSystem> fileSystem = null;
            ReferenceCountedDisposable<IFileSystem> tempFs = null;
            try
            {
                const int pathBufferLength = 0x40;

                // Hack around error CS8350.
                Span<byte> buffer = stackalloc byte[pathBufferLength];
                ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
                Span<byte> pathBuffer = MemoryMarshal.CreateSpan(ref bufferRef, pathBufferLength);

                if (storageId == CustomStorageId.System)
                {
                    Result rc = BaseFileSystemService.OpenBisFileSystem(out fileSystem, BisPartitionId.User);
                    if (rc.IsFailure()) return rc;

                    using var path = new Path();
                    var sb = new U8StringBuilder(pathBuffer);
                    sb.Append((byte)'/')
                        .Append(CustomStorage.GetCustomStorageDirectoryName(CustomStorageId.System));

                    rc = PathFunctions.SetUpFixedPath(ref path.Ref(), pathBuffer);
                    if (rc.IsFailure()) return rc;

                    tempFs = Shared.Move(ref fileSystem);
                    rc = Utility.WrapSubDirectory(out fileSystem, ref tempFs, in path, true);
                    if (rc.IsFailure()) return rc;
                }
                else if (storageId == CustomStorageId.SdCard)
                {
                    Result rc = BaseFileSystemService.OpenSdCardProxyFileSystem(out fileSystem);
                    if (rc.IsFailure()) return rc;

                    using var path = new Path();
                    var sb = new U8StringBuilder(pathBuffer);
                    sb.Append((byte)'/')
                        .Append(CommonPaths.SdCardNintendoRootDirectoryName)
                        .Append((byte)'/')
                        .Append(CustomStorage.GetCustomStorageDirectoryName(CustomStorageId.SdCard));

                    rc = PathFunctions.SetUpFixedPath(ref path.Ref(), pathBuffer);
                    if (rc.IsFailure()) return rc;

                    tempFs = Shared.Move(ref fileSystem);
                    rc = Utility.WrapSubDirectory(out fileSystem, ref tempFs, in path, true);
                    if (rc.IsFailure()) return rc;

                    tempFs = Shared.Move(ref fileSystem);
                    rc = FsCreators.EncryptedFileSystemCreator.Create(out fileSystem, ref tempFs,
                        IEncryptedFileSystemCreator.KeyId.CustomStorage, SdEncryptionSeed);
                    if (rc.IsFailure()) return rc;
                }
                else
                {
                    return ResultFs.InvalidArgument.Log();
                }

                outFileSystem = Shared.Move(ref fileSystem);
                return Result.Success;
            }
            finally
            {
                fileSystem?.Dispose();
                tempFs?.Dispose();
            }
        }

        private Result OpenHostFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);

            using var pathHost = new Path();
            Result rc = pathHost.Initialize(in path);
            if (rc.IsFailure()) return rc;

            rc = FsCreators.TargetManagerFileSystemCreator.NormalizeCaseOfPath(out bool isSupported, ref pathHost.Ref());
            if (rc.IsFailure()) return rc;

            rc = FsCreators.TargetManagerFileSystemCreator.Create(out fileSystem, in pathHost, isSupported, false,
                Result.Success);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public Result OpenHostFileSystem(out ReferenceCountedDisposable<IFileSystem> fileSystem, in Path path,
            bool openCaseSensitive)
        {
            if (!path.IsEmpty() && openCaseSensitive)
            {
                Result rc = OpenHostFileSystem(out fileSystem, in path);
                if (rc.IsFailure()) return rc;
            }
            else
            {
                Result rc = FsCreators.TargetManagerFileSystemCreator.Create(out fileSystem, in path, openCaseSensitive,
                    false, Result.Success);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        public Result SetSdCardEncryptionSeed(in EncryptionSeed seed)
        {
            SdEncryptionSeed = seed;
            return Result.Success;
        }
    }
}
