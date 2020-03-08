using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;

namespace LibHac.FsService.Creators
{
    public class SubDirectoryFileSystemCreator : ISubDirectoryFileSystemCreator
    {
        public Result Create(out IFileSystem subDirFileSystem, IFileSystem baseFileSystem, U8Span path)
        {
            subDirFileSystem = default;

            Result rc = baseFileSystem.OpenDirectory(out IDirectory _, path, OpenDirectoryMode.Directory);
            if (rc.IsFailure()) return rc;

            rc = SubdirectoryFileSystem.CreateNew(out SubdirectoryFileSystem fs, baseFileSystem, path.ToU8String());
            subDirFileSystem = fs;
            return rc;
        }
    }
}
