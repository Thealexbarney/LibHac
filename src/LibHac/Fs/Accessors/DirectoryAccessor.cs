using System.Collections.Generic;

namespace LibHac.Fs.Accessors
{
    public class DirectoryAccessor
    {
        private IDirectory Directory { get; }

        public FileSystemAccessor Parent { get; }

        public DirectoryAccessor(IDirectory baseDirectory, FileSystemAccessor parent)
        {
            Directory = baseDirectory;
            Parent = parent;
        }

        public IEnumerable<DirectoryEntry> Read()
        {
            return Directory.Read();
        }

        public int GetEntryCount()
        {
            return Directory.GetEntryCount();
        }
    }
}
