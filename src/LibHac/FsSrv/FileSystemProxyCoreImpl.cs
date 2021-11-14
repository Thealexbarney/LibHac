using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;

namespace LibHac.FsSrv;

public class FileSystemProxyCoreImpl
{
    private FileSystemCreatorInterfaces _fsCreators;
    private BaseFileSystemServiceImpl _baseFileSystemService;
    private EncryptionSeed _sdEncryptionSeed;

    public FileSystemProxyCoreImpl(FileSystemCreatorInterfaces fsCreators, BaseFileSystemServiceImpl baseFsService)
    {
        _fsCreators = fsCreators;
        _baseFileSystemService = baseFsService;
    }

    public Result OpenCloudBackupWorkStorageFileSystem(ref SharedRef<IFileSystem> outFileSystem,
        CloudBackupWorkStorageId storageId)
    {
        throw new NotImplementedException();
    }

    public Result OpenCustomStorageFileSystem(ref SharedRef<IFileSystem> outFileSystem, CustomStorageId storageId)
    {
        // Hack around error CS8350.
        const int pathBufferLength = 0x40;
        Span<byte> buffer = stackalloc byte[pathBufferLength];
        ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
        Span<byte> pathBuffer = MemoryMarshal.CreateSpan(ref bufferRef, pathBufferLength);

        using var fileSystem = new SharedRef<IFileSystem>();

        if (storageId == CustomStorageId.System)
        {
            Result rc = _baseFileSystemService.OpenBisFileSystem(ref fileSystem.Ref(), BisPartitionId.User);
            if (rc.IsFailure()) return rc;

            using var path = new Path();
            rc = PathFunctions.SetUpFixedPathSingleEntry(ref path.Ref(), pathBuffer,
                CustomStorage.GetCustomStorageDirectoryName(CustomStorageId.System));
            if (rc.IsFailure()) return rc;

            using SharedRef<IFileSystem> tempFs = SharedRef<IFileSystem>.CreateMove(ref fileSystem.Ref());
            rc = Utility.WrapSubDirectory(ref fileSystem.Ref(), ref tempFs.Ref(), in path, createIfMissing: true);
            if (rc.IsFailure()) return rc;
        }
        else if (storageId == CustomStorageId.SdCard)
        {
            Result rc = _baseFileSystemService.OpenSdCardProxyFileSystem(ref fileSystem.Ref());
            if (rc.IsFailure()) return rc;

            using var path = new Path();
            rc = PathFunctions.SetUpFixedPathDoubleEntry(ref path.Ref(), pathBuffer,
                CommonPaths.SdCardNintendoRootDirectoryName,
                CustomStorage.GetCustomStorageDirectoryName(CustomStorageId.System));
            if (rc.IsFailure()) return rc;

            using SharedRef<IFileSystem> tempFs = SharedRef<IFileSystem>.CreateMove(ref fileSystem.Ref());
            rc = Utility.WrapSubDirectory(ref fileSystem.Ref(), ref tempFs.Ref(), in path, createIfMissing: true);
            if (rc.IsFailure()) return rc;

            tempFs.SetByMove(ref fileSystem.Ref());
            rc = _fsCreators.EncryptedFileSystemCreator.Create(ref fileSystem.Ref(), ref tempFs.Ref(),
                IEncryptedFileSystemCreator.KeyId.CustomStorage, in _sdEncryptionSeed);
            if (rc.IsFailure()) return rc;
        }
        else
        {
            return ResultFs.InvalidArgument.Log();
        }

        outFileSystem.SetByMove(ref fileSystem.Ref());
        return Result.Success;
    }

    private Result OpenHostFileSystem(ref SharedRef<IFileSystem> outFileSystem, in Path path)
    {
        using var pathHost = new Path();
        Result rc = pathHost.Initialize(in path);
        if (rc.IsFailure()) return rc;

        rc = _fsCreators.TargetManagerFileSystemCreator.NormalizeCaseOfPath(out bool isSupported, ref pathHost.Ref());
        if (rc.IsFailure()) return rc;

        rc = _fsCreators.TargetManagerFileSystemCreator.Create(ref outFileSystem, in pathHost, isSupported,
            ensureRootPathExists: false, Result.Success);
        if (rc.IsFailure()) return rc;

        return Result.Success;
    }

    public Result OpenHostFileSystem(ref SharedRef<IFileSystem> outFileSystem, in Path path,
        bool openCaseSensitive)
    {
        if (!path.IsEmpty() && openCaseSensitive)
        {
            Result rc = OpenHostFileSystem(ref outFileSystem, in path);
            if (rc.IsFailure()) return rc;
        }
        else
        {
            Result rc = _fsCreators.TargetManagerFileSystemCreator.Create(ref outFileSystem, in path,
                openCaseSensitive, ensureRootPathExists: false, Result.Success);
            if (rc.IsFailure()) return rc;
        }

        return Result.Success;
    }

    public Result SetSdCardEncryptionSeed(in EncryptionSeed seed)
    {
        _sdEncryptionSeed = seed;
        return Result.Success;
    }
}
