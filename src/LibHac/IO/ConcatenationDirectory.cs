using System.Collections.Generic;

namespace LibHac.IO
{
    public class ConcatenationDirectory : IDirectory
    {
        IFileSystem IDirectory.ParentFileSystem => ParentFileSystem;
        public string FullPath { get; }
        public OpenDirectoryMode Mode { get; }

        private ConcatenationFileSystem ParentFileSystem { get; }
        private IDirectory ParentDirectory { get; }

        public ConcatenationDirectory(ConcatenationFileSystem fs, IDirectory parentDirectory, OpenDirectoryMode mode)
        {
            ParentFileSystem = fs;
            ParentDirectory = parentDirectory;
            Mode = mode;
            FullPath = parentDirectory.FullPath;
        }

        public IEnumerable<DirectoryEntry> Read()
        {
            foreach (DirectoryEntry entry in ParentDirectory.Read())
            {
                bool isSplit = ParentFileSystem.IsSplitFile(entry.FullPath);

                if (!CanReturnEntry(entry, isSplit)) continue;

                if (!isSplit)
                {
                    yield return entry;
                }
                else
                {
                    long size = ParentFileSystem.GetConcatenationFileSize(entry.FullPath);
                    yield return new DirectoryEntry(entry.Name, entry.FullPath, DirectoryEntryType.File, size);
                }
            }
        }

        public int GetEntryCount()
        {
            int count = 0;

            foreach (DirectoryEntry entry in ParentDirectory.Read())
            {
                bool isSplit = ParentFileSystem.IsSplitFile(entry.FullPath);

                if (CanReturnEntry(entry, isSplit)) count++;
            }

            return count;
        }

        private bool CanReturnEntry(DirectoryEntry entry, bool isSplit)
        {
            return Mode.HasFlag(OpenDirectoryMode.Files) && (entry.Type == DirectoryEntryType.File || isSplit) ||
                   Mode.HasFlag(OpenDirectoryMode.Directories) && entry.Type == DirectoryEntryType.Directory && !isSplit;
        }
    }
}
