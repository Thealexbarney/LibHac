using System;

namespace LibHac.FsSystem
{
    public class ReadOnlyFileSystem : IFileSystem
    {
        private IFileSystem BaseFs { get; }

        public ReadOnlyFileSystem(IFileSystem baseFileSystem)
        {
            BaseFs = baseFileSystem;
        }

        public Result OpenDirectory(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            return BaseFs.OpenDirectory(out directory, path, mode);
        }

        public Result OpenFile(out IFile file, string path, OpenMode mode)
        {
            file = default;

            Result rc = BaseFs.OpenFile(out IFile baseFile, path, mode);
            if (rc.IsFailure()) return rc;

            file = new ReadOnlyFile(baseFile);
            return Result.Success;
        }

        public Result GetEntryType(out DirectoryEntryType entryType, string path)
        {
            return BaseFs.GetEntryType(out entryType, path);
        }

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            freeSpace = 0;
            return Result.Success;

            // FS does:
            // return ResultFs.UnsupportedOperationReadOnlyFileSystemGetSpace.Log();
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            return BaseFs.GetTotalSpaceSize(out totalSpace, path);

            // FS does:
            // return ResultFs.UnsupportedOperationReadOnlyFileSystemGetSpace.Log();
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, string path)
        {
            return BaseFs.GetFileTimeStampRaw(out timeStamp, path);

            // FS does:
            // return ResultFs.NotImplemented.Log();
        }

        public Result Commit()
        {
            return Result.Success;
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            return ResultFs.NotImplemented.Log();
        }

        public Result CreateDirectory(string path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        public Result CreateFile(string path, long size, CreateFileOptions options) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        public Result DeleteDirectory(string path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        public Result DeleteDirectoryRecursively(string path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        public Result CleanDirectoryRecursively(string path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        public Result DeleteFile(string path) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        public Result RenameDirectory(string oldPath, string newPath) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();

        public Result RenameFile(string oldPath, string newPath) => ResultFs.UnsupportedOperationModifyReadOnlyFileSystem.Log();
    }
}
