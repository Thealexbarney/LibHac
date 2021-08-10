using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class ReadOnlyFileSystem : IFileSystem
    {
        private IFileSystem BaseFs { get; }
        private ReferenceCountedDisposable<IFileSystem> BaseFsShared { get; }

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

        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
            OpenDirectoryMode mode)
        {
            return BaseFs.OpenDirectory(ref outDirectory, in path, mode);
        }

        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
        {
            using var baseFile = new UniqueRef<IFile>();
            Result rc = BaseFs.OpenFile(ref baseFile.Ref(), in path, mode);
            if (rc.IsFailure()) return rc;

            outFile.Reset(new ReadOnlyFile(ref baseFile.Ref()));
            return Result.Success;
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
        {
            return BaseFs.GetEntryType(out entryType, in path);
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
        {
            return BaseFs.GetFreeSpaceSize(out freeSpace, in path);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
        {
            return BaseFs.GetTotalSpaceSize(out totalSpace, in path);

            // FS does:
            // return ResultFs.UnsupportedOperationReadOnlyFileSystemGetSpace.Log();
        }

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
        {
            return BaseFs.GetFileTimeStampRaw(out timeStamp, in path);

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
