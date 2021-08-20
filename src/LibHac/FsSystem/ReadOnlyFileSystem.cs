using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class ReadOnlyFileSystem : IFileSystem
    {
        private SharedRef<IFileSystem> _baseFileSystem;

        public ReadOnlyFileSystem(ref SharedRef<IFileSystem> baseFileSystem)
        {
            _baseFileSystem = SharedRef<IFileSystem>.CreateMove(ref baseFileSystem);

            Assert.SdkRequires(_baseFileSystem.HasValue);
        }

        public override void Dispose()
        {
            _baseFileSystem.Destroy();
            base.Dispose();
        }

        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
            OpenDirectoryMode mode)
        {
            return _baseFileSystem.Get.OpenDirectory(ref outDirectory, in path, mode);
        }

        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
        {
            using var baseFile = new UniqueRef<IFile>();
            Result rc = _baseFileSystem.Get.OpenFile(ref baseFile.Ref(), in path, mode);
            if (rc.IsFailure()) return rc;

            outFile.Reset(new ReadOnlyFile(ref baseFile.Ref()));
            return Result.Success;
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
        {
            return _baseFileSystem.Get.GetEntryType(out entryType, in path);
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
        {
            return _baseFileSystem.Get.GetFreeSpaceSize(out freeSpace, in path);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
        {
            return _baseFileSystem.Get.GetTotalSpaceSize(out totalSpace, in path);

            // FS does:
            // return ResultFs.UnsupportedOperationReadOnlyFileSystemGetSpace.Log();
        }

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
        {
            return _baseFileSystem.Get.GetFileTimeStampRaw(out timeStamp, in path);

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
    }
}
