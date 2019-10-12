using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class ReadOnlyFileSystem : FileSystemBase
    {
        private IFileSystem BaseFs { get; }

        public ReadOnlyFileSystem(IFileSystem baseFileSystem)
        {
            BaseFs = baseFileSystem;
        }

        protected override Result OpenDirectoryImpl(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            return BaseFs.OpenDirectory(out directory, path, mode);
        }

        protected override Result OpenFileImpl(out IFile file, string path, OpenMode mode)
        {
            file = default;

            Result rc = BaseFs.OpenFile(out IFile baseFile, path, mode);
            if (rc.IsFailure()) return rc;

            file = new ReadOnlyFile(baseFile);
            return Result.Success;
        }

        protected override Result GetEntryTypeImpl(out DirectoryEntryType entryType, string path)
        {
            return BaseFs.GetEntryType(out entryType, path);
        }

        protected override Result GetFreeSpaceSizeImpl(out long freeSpace, string path)
        {
            freeSpace = 0;
            return Result.Success;

            // FS does:
            // return ResultFs.UnsupportedOperationReadOnlyFileSystemGetSpace.Log();
        }

        protected override Result GetTotalSpaceSizeImpl(out long totalSpace, string path)
        {
            return BaseFs.GetTotalSpaceSize(out totalSpace, path);

            // FS does:
            // return ResultFs.UnsupportedOperationReadOnlyFileSystemGetSpace.Log();
        }

        protected override Result GetFileTimeStampRawImpl(out FileTimeStampRaw timeStamp, string path)
        {
            return BaseFs.GetFileTimeStampRaw(out timeStamp, path);

            // FS does:
            // return ResultFs.NotImplemented.Log();
        }

        protected override Result CommitImpl()
        {
            return Result.Success;
        }

        protected override Result CreateDirectoryImpl(string path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result CreateFileImpl(string path, long size, CreateFileOptions options) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result DeleteDirectoryImpl(string path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result DeleteDirectoryRecursivelyImpl(string path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result CleanDirectoryRecursivelyImpl(string path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result DeleteFileImpl(string path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result RenameDirectoryImpl(string oldPath, string newPath) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result RenameFileImpl(string oldPath, string newPath) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();
    }
}
