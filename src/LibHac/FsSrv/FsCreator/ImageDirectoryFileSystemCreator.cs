using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Impl;
using LibHac.FsSystem;
using Utility = LibHac.FsSystem.Utility;

namespace LibHac.FsSrv.FsCreator;

public class ImageDirectoryFileSystemCreator : IBaseFileSystemCreator
{
    private Configuration _config;

    public struct Configuration
    {
        public IBuiltInStorageFileSystemCreator BisFileSystemCreator;
        public ISdCardProxyFileSystemCreator SdCardFileSystemCreator;
        public ILocalFileSystemCreator LocalFileSystemCreator;
        public ISubDirectoryFileSystemCreator SubDirectoryFileSystemCreator;
    }

    public ImageDirectoryFileSystemCreator(in Configuration config)
    {
        _config = config;
    }

    public Result Create(ref SharedRef<IFileSystem> outFileSystem, BaseFileSystemId id)
    {
        Result res;

        using var fileSystem = new SharedRef<IFileSystem>();
        using scoped var imageDirectoryPath = new Path();
        Span<byte> imageDirectoryPathBuffer = stackalloc byte[0x40];

        // Open the base file system containing the image directory, and get the path of the image directory
        switch (id)
        {
            case BaseFileSystemId.ImageDirectoryNand:
            {
                res = _config.BisFileSystemCreator.Create(ref fileSystem.Ref, BisPartitionId.User);
                if (res.IsFailure()) return res.Miss();

                res = PathFunctions.SetUpFixedPathSingleEntry(ref imageDirectoryPath.Ref(), imageDirectoryPathBuffer,
                    CommonDirNames.ImageDirectoryName);
                if (res.IsFailure()) return res.Miss();

                break;
            }
            case BaseFileSystemId.ImageDirectorySdCard:
            {
                res = _config.SdCardFileSystemCreator.Create(ref fileSystem.Ref);
                if (res.IsFailure()) return res.Miss();

                res = PathFunctions.SetUpFixedPathDoubleEntry(ref imageDirectoryPath.Ref(), imageDirectoryPathBuffer,
                    CommonDirNames.SdCardNintendoRootDirectoryName, CommonDirNames.ImageDirectoryName);
                if (res.IsFailure()) return res.Miss();

                break;
            }
            default:
                return ResultFs.InvalidArgument.Log();
        }

        res = Utility.EnsureDirectory(fileSystem.Get, in imageDirectoryPath);
        if (res.IsFailure()) return res.Miss();

        using var subDirFs = new SharedRef<IFileSystem>();
        res = _config.SubDirectoryFileSystemCreator.Create(ref subDirFs.Ref, in fileSystem, in imageDirectoryPath);
        if (res.IsFailure()) return res.Miss();

        const StorageLayoutType storageFlag = StorageLayoutType.NonGameCard;
        using var typeSetFileSystem = new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(in subDirFs, storageFlag));
        using var asyncFileSystem = new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(in typeSetFileSystem));

        outFileSystem.SetByMove(ref asyncFileSystem.Ref);

        return Result.Success;
    }

    public Result Format(BaseFileSystemId id)
    {
        return ResultFs.NotImplemented.Log();
    }
}