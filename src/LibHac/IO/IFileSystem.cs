using System;

namespace LibHac.IO
{
    public interface IFileSystem
    {
        void CreateDirectory(string path);
        void CreateFile(string path, long size, CreateFileOptions options);
        void DeleteDirectory(string path);
        void DeleteFile(string path);
        IDirectory OpenDirectory(string path, OpenDirectoryMode mode);
        IFile OpenFile(string path, OpenMode mode);
        void RenameDirectory(string srcPath, string dstPath);
        void RenameFile(string srcPath, string dstPath);
        bool DirectoryExists(string path);
        bool FileExists(string path);
        DirectoryEntryType GetEntryType(string path);
        void Commit();
    }

    [Flags]
    public enum OpenDirectoryMode
    {
        Directories = 1,
        Files = 2,
        All = Directories | Files
    }

    [Flags]
    public enum CreateFileOptions
    { 
        None = 0,
        CreateConcatenationFile = 1 << 0
    }
}