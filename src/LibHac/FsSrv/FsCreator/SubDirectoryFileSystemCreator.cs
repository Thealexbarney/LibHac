using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.FsCreator;

public class SubDirectoryFileSystemCreator : ISubDirectoryFileSystemCreator
{
    public Result Create(ref SharedRef<IFileSystem> outSubDirFileSystem, ref SharedRef<IFileSystem> baseFileSystem,
        in Path path)
    {
        using var directory = new UniqueRef<IDirectory>();

        Result rc = baseFileSystem.Get.OpenDirectory(ref directory.Ref(), in path, OpenDirectoryMode.Directory);
        if (rc.IsFailure()) return rc;

        directory.Reset();

        using var subFs = new SharedRef<SubdirectoryFileSystem>(new SubdirectoryFileSystem(ref baseFileSystem));

        if (!subFs.HasValue)
            return ResultFs.AllocationMemoryFailedInSubDirectoryFileSystemCreatorA.Log();

        rc = subFs.Get.Initialize(in path);
        if (rc.IsFailure()) return rc;

        outSubDirFileSystem.SetByMove(ref subFs.Ref());
        return Result.Success;
    }
}
