using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.FsCreator
{
    public class SubDirectoryFileSystemCreator : ISubDirectoryFileSystemCreator
    {
        public Result Create(out ReferenceCountedDisposable<IFileSystem> subDirFileSystem,
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out subDirFileSystem);

            using var directory = new UniqueRef<IDirectory>();

            Result rc = baseFileSystem.Target.OpenDirectory(ref directory.Ref(), in path, OpenDirectoryMode.Directory);
            if (rc.IsFailure()) return rc;

            directory.Reset();

            ReferenceCountedDisposable<SubdirectoryFileSystem> subFs = null;
            try
            {
                subFs = new ReferenceCountedDisposable<SubdirectoryFileSystem>(
                    new SubdirectoryFileSystem(ref baseFileSystem));

                rc = subFs.Target.Initialize(in path);
                if (rc.IsFailure()) return rc;

                subDirFileSystem = subFs.AddReference<IFileSystem>();
                return Result.Success;
            }
            finally
            {
                subFs?.Dispose();
            }
        }
    }
}
