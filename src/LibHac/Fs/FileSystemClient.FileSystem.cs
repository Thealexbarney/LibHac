using LibHac.FsSystem;

namespace LibHac.Fs
{
    public partial class FileSystemClient
    {
        public Result CreateDirectory(string path)
        {
            return FsManager.CreateDirectory(path);
        }

        public Result CreateFile(string path, long size)
        {
            return CreateFile(path, size, CreateFileOptions.None);
        }

        public Result CreateFile(string path, long size, CreateFileOptions options)
        {
            return FsManager.CreateFile(path, size, options);
        }

        public Result DeleteDirectory(string path)
        {
            return FsManager.DeleteDirectory(path);
        }

        public Result DeleteDirectoryRecursively(string path)
        {
            return FsManager.DeleteDirectoryRecursively(path);
        }

        public Result CleanDirectoryRecursively(string path)
        {
            return FsManager.CleanDirectoryRecursively(path);
        }

        public Result DeleteFile(string path)
        {
            return FsManager.DeleteFile(path);
        }

        public Result RenameDirectory(string oldPath, string newPath)
        {
            return FsManager.RenameDirectory(oldPath, newPath);
        }

        public Result RenameFile(string oldPath, string newPath)
        {
            return FsManager.RenameFile(oldPath, newPath);
        }

        public Result GetEntryType(out DirectoryEntryType type, string path)
        {
            return FsManager.GetEntryType(out type, path);
        }

        public Result OpenFile(out FileHandle handle, string path, OpenMode mode)
        {
            return FsManager.OpenFile(out handle, path, mode);
        }

        public Result OpenDirectory(out DirectoryHandle handle, string path, OpenDirectoryMode mode)
        {
            return FsManager.OpenDirectory(out handle, path, mode);
        }

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            return FsManager.GetFreeSpaceSize(out freeSpace, path);
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            return FsManager.GetTotalSpaceSize(out totalSpace, path);
        }

        public Result Commit(string mountName)
        {
            return FsManager.Commit(mountName);
        }
    }
}