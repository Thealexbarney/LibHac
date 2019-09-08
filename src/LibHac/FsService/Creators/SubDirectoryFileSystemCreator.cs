using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public class SubDirectoryFileSystemCreator : ISubDirectoryFileSystemCreator
    {
        public Result Create(out IFileSystem subDirFileSystem, IFileSystem baseFileSystem, string path)
        {
            subDirFileSystem = default;

            Result rc = baseFileSystem.OpenDirectory(out IDirectory _, path, OpenDirectoryMode.Directory);
            if (rc.IsFailure()) return rc;

            subDirFileSystem = new SubdirectoryFileSystem(baseFileSystem, path);

            return Result.Success;
        }
    }
}
