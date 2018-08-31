using System.IO;

namespace LibHac
{
    public class FileSystem : IFileSystem
    {
        public string Root { get; }

        public FileSystem(string rootDir)
        {
            Root = Path.GetFullPath(rootDir);
        }

        public bool FileExists(string path)
        {
            return File.Exists(Path.Combine(Root, path));
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(Path.Combine(Root, path));
        }

        public Stream OpenFile(string path, FileMode mode)
        {
            return new FileStream(path, mode);
        }

        public Stream OpenFile(string path, FileMode mode, FileAccess access)
        {
            return new FileStream(path, mode, access);
        }

        public string[] GetFileSystemEntries(string path, string searchPattern)
        {
            return Directory.GetFileSystemEntries(Path.Combine(Root, path), searchPattern);
        }

        public string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.GetFileSystemEntries(Path.Combine(Root, path), searchPattern, searchOption);
        }

        public string GetFullPath(string path)
        {
            return Path.Combine(Root, path);
        }
    }
}
