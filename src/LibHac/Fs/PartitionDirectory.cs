using System.Collections.Generic;
using System.IO;

namespace LibHac.Fs
{
    public class PartitionDirectory : IDirectory
    {
        IFileSystem IDirectory.ParentFileSystem => ParentFileSystem;
        public PartitionFileSystem ParentFileSystem { get; }
        public string FullPath { get; }

        public OpenDirectoryMode Mode { get; }

        public PartitionDirectory(PartitionFileSystem fs, string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            if (path != "/") throw new DirectoryNotFoundException();

            ParentFileSystem = fs;
            FullPath = path;
            Mode = mode;
        }


        public IEnumerable<DirectoryEntry> Read()
        {
            if (Mode.HasFlag(OpenDirectoryMode.Files))
            {
                foreach (PartitionFileEntry entry in ParentFileSystem.Files)
                {
                    yield return new DirectoryEntry(entry.Name, '/' + entry.Name, DirectoryEntryType.File, entry.Size);
                }
            }
        }

        public int GetEntryCount()
        {
            int count = 0;

            if (Mode.HasFlag(OpenDirectoryMode.Files))
            {
                count += ParentFileSystem.Files.Length;
            }

            return count;
        }
    }
}