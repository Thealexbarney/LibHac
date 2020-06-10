using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsService.Creators
{
    public class SubDirectoryFileSystemCreator : ISubDirectoryFileSystemCreator
    {
        public Result Create(out IFileSystem subDirFileSystem, IFileSystem baseFileSystem, U8Span path)
        {
            return Create(out subDirFileSystem, baseFileSystem, path, false);
        }

        public Result Create(out IFileSystem subDirFileSystem, IFileSystem baseFileSystem, U8Span path, bool preserveUnc)
        {
            subDirFileSystem = default;

            Result rc = baseFileSystem.OpenDirectory(out IDirectory _, path, OpenDirectoryMode.Directory);
            if (rc.IsFailure()) return rc;

            rc = SubdirectoryFileSystem.CreateNew(out SubdirectoryFileSystem fs, baseFileSystem, path.ToU8String(), preserveUnc);
            subDirFileSystem = fs;
            return rc;
        }
    }
}
