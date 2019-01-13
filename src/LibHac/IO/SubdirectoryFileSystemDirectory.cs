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
            foreach (DirectoryEntry entry in BaseDirectory.Read())
            {
                yield return new DirectoryEntry(entry.Name, FullPath + '/' + entry.Name, entry.Type, entry.Size);
            }
        }

        public int GetEntryCount()
        {
            return BaseDirectory.GetEntryCount();
        }
    }
}
