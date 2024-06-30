using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Impl;
using LibHac.FsSystem;
using Utility = LibHac.FsSystem.Utility;

namespace LibHac.FsSrv.FsCreator;

public class BisFsSubDirectoryFileSystemCreator : IBaseFileSystemCreator
{
    private IBuiltInStorageFileSystemCreator _bisFileSystemCreator;
    private BisPartitionId _partitionId;
    private ISubDirectoryFileSystemCreator _subDirectoryFileSystemCreator;
    private U8String _path;

    public BisFsSubDirectoryFileSystemCreator(IBuiltInStorageFileSystemCreator bisFileSystemCreator,
        BisPartitionId partitionId, ISubDirectoryFileSystemCreator subDirectoryFileSystemCreator, U8Span path)
    {
        _bisFileSystemCreator = bisFileSystemCreator;
        _partitionId = partitionId;
        _subDirectoryFileSystemCreator = subDirectoryFileSystemCreator;
        _path = path.ToU8String();
    }

    public Result Create(ref SharedRef<IFileSystem> outFileSystem, BaseFileSystemId id)
    {
        using var fileSystem = new SharedRef<IFileSystem>();

        using var subDirPath = new Path();
        Result res = PathFunctions.SetUpFixedPath(ref subDirPath.Ref(), _path);
        if (res.IsFailure()) return res.Miss();
        
        // Open the base file system
        res = _bisFileSystemCreator.Create(ref fileSystem.Ref, _partitionId);
        if (res.IsFailure()) return res.Miss();

        // Ensure the subdirectory exists and get an IFileSystem over it
        res = Utility.EnsureDirectory(fileSystem.Get, in subDirPath);
        if (res.IsFailure()) return res.Miss();
        
        using var subDirFs = new SharedRef<IFileSystem>();
        res = _subDirectoryFileSystemCreator.Create(ref subDirFs.Ref, in fileSystem, in subDirPath);
        if (res.IsFailure()) return res.Miss();
         
        // Add all the file system wrappers
        using var typeSetFileSystem = new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(in fileSystem, StorageLayoutType.Bis));
        using var asyncFileSystem = new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(in typeSetFileSystem));

        outFileSystem.SetByMove(ref asyncFileSystem.Ref);

        return Result.Success;
    }

    public Result Format(BaseFileSystemId id)
    {
        return ResultFs.NotImplemented.Log();
    }
}