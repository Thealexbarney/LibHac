using System.IO;

namespace LibHac
{
    public interface IFileSystemOld
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);
        Stream OpenFile(string path, FileMode mode);
        Stream OpenFile(string path, FileMode mode, FileAccess access);
        string[] GetFileSystemEntries(string path, string searchPattern);
        string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption);
        string GetFullPath(string path);
    }
}
