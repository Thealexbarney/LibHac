using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Creators
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

        public Result Create(out ReferenceCountedDisposable<IFileSystem> subDirFileSystem,
            ReferenceCountedDisposable<IFileSystem> baseFileSystem, U8Span path)
        {
            return Create(out subDirFileSystem, baseFileSystem, path, false);
        }

        public Result Create(out ReferenceCountedDisposable<IFileSystem> subDirFileSystem,
            ReferenceCountedDisposable<IFileSystem> baseFileSystem, U8Span path, bool preserveUnc)
        {
            subDirFileSystem = default;

            // Verify the sub-path exists
            Result rc = baseFileSystem.Target.OpenDirectory(out IDirectory _, path, OpenDirectoryMode.Directory);
            if (rc.IsFailure()) return rc;

            // Initialize the SubdirectoryFileSystem
            var subDir = new SubdirectoryFileSystem(baseFileSystem, preserveUnc);
            using var subDirShared = new ReferenceCountedDisposable<SubdirectoryFileSystem>(subDir);

            rc = subDirShared.Target.Initialize(path);
            if (rc.IsFailure()) return rc;

            subDirFileSystem = subDirShared.AddReference<IFileSystem>();
            return Result.Success;
        }
    }
}
