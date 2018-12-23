using System;

namespace LibHac.IO
{
    public interface IFileSystem
    {
        void Commit();
        void CreateDirectory(string path);
        void CreateFile(string path, long size);
        void DeleteDirectory(string path);
        void DeleteFile(string path);
        IDirectory OpenDirectory(string path, OpenDirectoryMode mode);
        IFile OpenFile(string path);
        void RenameDirectory(string srcPath, string dstPath);
        void RenameFile(string srcPath, string dstPath);
        bool DirectoryExists(string path);
        bool FileExists(string path);
    }

    [Flags]
    public enum OpenDirectoryMode
    {
        Directories = 1,
        Files = 2,
        All = Directories | Files
    }
}