using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class ReadOnlyFileSystem : IFileSystem
    {
        private IFileSystem BaseFs { get; }
        private ReferenceCountedDisposable<IFileSystem> BaseFsShared { get; }

        // Todo: Remove non-shared constructor
        public ReadOnlyFileSystem(IFileSystem baseFileSystem)
        {
            BaseFs = baseFileSystem;
        }

        public ReadOnlyFileSystem(ReferenceCountedDisposable<IFileSystem> baseFileSystem)
        {
            BaseFsShared = baseFileSystem;
            BaseFs = BaseFsShared.Target;
        }

        public static ReferenceCountedDisposable<IFileSystem> CreateShared(
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem)
        {
            var fs = new ReadOnlyFileSystem(Shared.Move(ref baseFileSystem));
            return new ReferenceCountedDisposable<IFileSystem>(fs);
        }

        protected override Result DoOpenDirectory(out IDirectory directory, in Path path, OpenDirectoryMode mode)
        {
            return BaseFs.OpenDirectory(out directory, path, mode);
        }

        protected override Result DoOpenFile(out IFile file, in Path path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            Result rc = BaseFs.OpenFile(out IFile baseFile, path, mode);
            if (rc.IsFailure()) return rc;

            file = new ReadOnlyFile(baseFile);
            return Result.Success;
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
        {
            return BaseFs.GetEntryType(out entryType, path);
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
        {
            return BaseFs.GetFreeSpaceSize(out freeSpace, path);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
        {
            return BaseFs.GetTotalSpaceSize(out totalSpace, path);

            // FS does:
            // return ResultFs.UnsupportedOperationReadOnlyFileSystemGetSpace.Log();
        }

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
        {
            return BaseFs.GetFileTimeStampRaw(out timeStamp, path);

            // FS does:
            // return ResultFs.NotImplemented.Log();
        }

        protected override Result DoCommit()
        {
            return Result.Success;
        }

        protected override Result DoCreateDirectory(in Path path) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoDeleteDirectory(in Path path) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoDeleteDirectoryRecursively(in Path path) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoCleanDirectoryRecursively(in Path path) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoDeleteFile(in Path path) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoRenameDirectory(in Path currentPath, in Path newPath) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoRenameFile(in Path currentPath, in Path newPath) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        public override void Dispose()
        {
            BaseFsShared?.Dispose();
            base.Dispose();
        }
    }
}
