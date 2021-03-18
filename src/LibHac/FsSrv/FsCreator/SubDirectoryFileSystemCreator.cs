using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.FsCreator
{
    public class SubDirectoryFileSystemCreator : ISubDirectoryFileSystemCreator
    {
        public Result Create(out ReferenceCountedDisposable<IFileSystem> subDirFileSystem,
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, U8Span path)
        {
            return Create(out subDirFileSystem, ref baseFileSystem, path, false);
        }

        public Result Create(out ReferenceCountedDisposable<IFileSystem> subDirFileSystem,
           ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, U8Span path, bool preserveUnc)
        {
            UnsafeHelpers.SkipParamInit(out subDirFileSystem);

            // Verify the sub-path exists
            Result rc = baseFileSystem.Target.OpenDirectory(out IDirectory _, path, OpenDirectoryMode.Directory);
            if (rc.IsFailure()) return rc;

            // Initialize the SubdirectoryFileSystem
            var subDir = new SubdirectoryFileSystem(ref baseFileSystem, preserveUnc);
            using var subDirShared = new ReferenceCountedDisposable<SubdirectoryFileSystem>(subDir);

            rc = subDirShared.Target.Initialize(path);
            if (rc.IsFailure()) return rc;

            subDirFileSystem = subDirShared.AddReference<IFileSystem>();
            return Result.Success;
        }
    }
}
