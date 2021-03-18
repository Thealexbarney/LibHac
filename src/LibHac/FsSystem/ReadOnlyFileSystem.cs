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

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            return BaseFs.OpenDirectory(out directory, path, mode);
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            Result rc = BaseFs.OpenFile(out IFile baseFile, path, mode);
            if (rc.IsFailure()) return rc;

            file = new ReadOnlyFile(baseFile);
            return Result.Success;
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            return BaseFs.GetEntryType(out entryType, path);
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            return BaseFs.GetFreeSpaceSize(out freeSpace, path);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            return BaseFs.GetTotalSpaceSize(out totalSpace, path);

            // FS does:
            // return ResultFs.UnsupportedOperationReadOnlyFileSystemGetSpace.Log();
        }

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
        {
            return BaseFs.GetFileTimeStampRaw(out timeStamp, path);

            // FS does:
            // return ResultFs.NotImplemented.Log();
        }

        protected override Result DoCommit()
        {
            return Result.Success;
        }

        protected override Result DoCreateDirectory(U8Span path) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions options) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoDeleteDirectory(U8Span path) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoDeleteDirectoryRecursively(U8Span path) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoCleanDirectoryRecursively(U8Span path) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoDeleteFile(U8Span path) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath) => ResultFs.UnsupportedWriteForReadOnlyFileSystem.Log();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BaseFsShared?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
