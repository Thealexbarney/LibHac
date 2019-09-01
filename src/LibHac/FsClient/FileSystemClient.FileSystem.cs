using System;
using LibHac.Fs;

namespace LibHac.FsClient
{
    public partial class FileSystemClient
    {
        public Result CreateDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public Result CreateFile(string path, long size)
        {
            throw new NotImplementedException();
        }

        public Result CreateFile(string path, long size, CreateFileOptions options)
        {
            throw new NotImplementedException();
        }

        public Result DeleteDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public Result DeleteDirectoryRecursively(string path)
        {
            throw new NotImplementedException();
        }

        public Result CleanDirectoryRecursively(string path)
        {
            throw new NotImplementedException();
        }

        public Result DeleteFile(string path)
        {
            throw new NotImplementedException();
        }

        public Result RenameDirectory(string oldPath, string newPath)
        {
            throw new NotImplementedException();
        }

        public Result RenameFile(string oldPath, string newPath)
        {
            throw new NotImplementedException();
        }

        public Result GetEntryType(out DirectoryEntryType type, string path)
        {
            throw new NotImplementedException();
        }

        public FileHandle OpenFile(out FileHandle handle, string path, OpenMode mode)
        {
            throw new NotImplementedException();
        }

        public DirectoryHandle OpenDirectory(out DirectoryHandle handle, string path, OpenDirectoryMode mode)
        {
            throw new NotImplementedException();
        }

        public Result GetFreeSpaceSize(out long size, string path)
        {
            throw new NotImplementedException();
        }

        public Result GetTotalSpaceSize(out long size, string path)
        {
            throw new NotImplementedException();
        }

        public Result Commit(string mountName)
        {
            throw new NotImplementedException();
        }
    }
}