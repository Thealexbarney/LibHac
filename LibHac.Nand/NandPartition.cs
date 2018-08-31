using System.IO;
using System.Linq;
using DiscUtils.Fat;

namespace LibHac.Nand
{
    public class NandPartition : IFileSystem
    {
        public FatFileSystem Fs { get; }

        public NandPartition(FatFileSystem fileSystem)
        {
            Fs = fileSystem;
        }

        public bool FileExists(string path)
        {
            return Fs.FileExists(path);
        }

        public bool DirectoryExists(string path)
        {
            return Fs.DirectoryExists(path);
        }

        public Stream OpenFile(string path, FileMode mode)
        {
            return Fs.OpenFile(path, mode);
        }

        public Stream OpenFile(string path, FileMode mode, FileAccess access)
        {
            return Fs.OpenFile(path, mode, access);
        }

        public string[] GetFileSystemEntries(string path, string searchPattern)
        {
            return Fs.GetFileSystemEntries(path, searchPattern);
        }

        public string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
            var files = Fs.GetFiles(path, searchPattern, searchOption);
            var dirs = Fs.GetDirectories(path, searchPattern, searchOption);
            return files.Concat(dirs).ToArray();
        }

        public string GetFullPath(string path)
        {
            return path;
        }
    }
}
