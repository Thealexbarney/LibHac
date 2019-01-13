using System.Collections.Generic;

namespace LibHac.IO
{
    public class SubdirectoryFileSystemDirectory : IDirectory
    {
        public SubdirectoryFileSystemDirectory(SubdirectoryFileSystem fs, IDirectory baseDir, string path, OpenDirectoryMode mode)
        {
            ParentFileSystem = fs;
            BaseDirectory = baseDir;
            FullPath = path;
            Mode = mode;
        }

        public IFileSystem ParentFileSystem { get; }
        public string FullPath { get; }
        public OpenDirectoryMode Mode { get; }

        private IDirectory BaseDirectory { get; }

        public IEnumerable<DirectoryEntry> Read()
        {
            return BaseDirectory.Read();
        }

        public int GetEntryCount()
        {
            return BaseDirectory.GetEntryCount();
        }
    }
}
