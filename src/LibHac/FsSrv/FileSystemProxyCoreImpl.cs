using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSrv.FsCreator;

using Utility = LibHac.FsSrv.Impl.Utility;

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
        Unsafe.SkipInit(out Array64<byte> pathBuffer);

        using var fileSystem = new SharedRef<IFileSystem>();

        if (storageId == CustomStorageId.System)
        {
            Result res = _baseFileSystemService.OpenBisFileSystem(ref fileSystem.Ref(), BisPartitionId.User);
            if (res.IsFailure()) return res.Miss();

            using var path = new Path();
            res = PathFunctions.SetUpFixedPathSingleEntry(ref path.Ref(), pathBuffer.Items,
                CustomStorage.GetCustomStorageDirectoryName(CustomStorageId.System));
            if (res.IsFailure()) return res.Miss();

            using SharedRef<IFileSystem> tempFs = SharedRef<IFileSystem>.CreateMove(ref fileSystem.Ref());
            res = Utility.WrapSubDirectory(ref fileSystem.Ref(), ref tempFs.Ref(), in path, createIfMissing: true);
            if (res.IsFailure()) return res.Miss();
        }
        else if (storageId == CustomStorageId.SdCard)
        {
            Result res = _baseFileSystemService.OpenSdCardProxyFileSystem(ref fileSystem.Ref());
            if (res.IsFailure()) return res.Miss();

            using var path = new Path();
            res = PathFunctions.SetUpFixedPathDoubleEntry(ref path.Ref(), pathBuffer.Items,
                CommonPaths.SdCardNintendoRootDirectoryName,
                CustomStorage.GetCustomStorageDirectoryName(CustomStorageId.System));
            if (res.IsFailure()) return res.Miss();

            using SharedRef<IFileSystem> tempFs = SharedRef<IFileSystem>.CreateMove(ref fileSystem.Ref());
            res = Utility.WrapSubDirectory(ref fileSystem.Ref(), ref tempFs.Ref(), in path, createIfMissing: true);
            if (res.IsFailure()) return res.Miss();

            tempFs.SetByMove(ref fileSystem.Ref());
            res = _fsCreators.EncryptedFileSystemCreator.Create(ref fileSystem.Ref(), ref tempFs.Ref(),
                IEncryptedFileSystemCreator.KeyId.CustomStorage, in _sdEncryptionSeed);
            if (res.IsFailure()) return res.Miss();
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
        Result res = pathHost.Initialize(in path);
        if (res.IsFailure()) return res.Miss();

        res = _fsCreators.TargetManagerFileSystemCreator.NormalizeCaseOfPath(out bool isSupported, ref pathHost.Ref());
        if (res.IsFailure()) return res.Miss();

        res = _fsCreators.TargetManagerFileSystemCreator.Create(ref outFileSystem, in pathHost, isSupported,
            ensureRootPathExists: false, Result.Success);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result OpenHostFileSystem(ref SharedRef<IFileSystem> outFileSystem, in Path path,
        bool openCaseSensitive)
    {
        if (!path.IsEmpty() && openCaseSensitive)
        {
            Result res = OpenHostFileSystem(ref outFileSystem, in path);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            Result res = _fsCreators.TargetManagerFileSystemCreator.Create(ref outFileSystem, in path,
                openCaseSensitive, ensureRootPathExists: false, Result.Success);
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public Result SetSdCardEncryptionSeed(in EncryptionSeed seed)
    {
        _sdEncryptionSeed = seed;
        return Result.Success;
    }
}