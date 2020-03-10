using LibHac.Common;
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

        protected override Result OpenDirectoryImpl(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            return BaseFs.OpenDirectory(out directory, path, mode);
        }

        protected override Result OpenFileImpl(out IFile file, U8Span path, OpenMode mode)
        {
            file = default;

            Result rc = BaseFs.OpenFile(out IFile baseFile, path, mode);
            if (rc.IsFailure()) return rc;

            file = new ReadOnlyFile(baseFile);
            return Result.Success;
        }

        protected override Result GetEntryTypeImpl(out DirectoryEntryType entryType, U8Span path)
        {
            return BaseFs.GetEntryType(out entryType, path);
        }

        protected override Result GetFreeSpaceSizeImpl(out long freeSpace, U8Span path)
        {
            freeSpace = 0;
            return Result.Success;

            // FS does:
            // return ResultFs.UnsupportedOperationReadOnlyFileSystemGetSpace.Log();
        }

        protected override Result GetTotalSpaceSizeImpl(out long totalSpace, U8Span path)
        {
            return BaseFs.GetTotalSpaceSize(out totalSpace, path);

            // FS does:
            // return ResultFs.UnsupportedOperationReadOnlyFileSystemGetSpace.Log();
        }

        protected override Result GetFileTimeStampRawImpl(out FileTimeStampRaw timeStamp, U8Span path)
        {
            return BaseFs.GetFileTimeStampRaw(out timeStamp, path);

            // FS does:
            // return ResultFs.NotImplemented.Log();
        }

        protected override Result CommitImpl()
        {
            return Result.Success;
        }

        protected override Result CreateDirectoryImpl(U8Span path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result CreateFileImpl(U8Span path, long size, CreateFileOptions options) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result DeleteDirectoryImpl(U8Span path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result DeleteDirectoryRecursivelyImpl(U8Span path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result CleanDirectoryRecursivelyImpl(U8Span path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result DeleteFileImpl(U8Span path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result RenameDirectoryImpl(U8Span oldPath, U8Span newPath) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        protected override Result RenameFileImpl(U8Span oldPath, U8Span newPath) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();
    }
}
