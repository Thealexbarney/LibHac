using System.Collections.Generic;

#if CROSS_PLATFORM
using System.Runtime.InteropServices;
#endif

namespace LibHac.Fs
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
                bool isSplit = IsConcatenationFile(entry);

                if (!CanReturnEntry(entry, isSplit)) continue;

                if (isSplit)
                {
                    entry.Type = DirectoryEntryType.File;
                    entry.Size = ParentFileSystem.GetConcatenationFileSize(entry.FullPath);
                    entry.Attributes = NxFileAttributes.None;
                }

                yield return entry;
            }
        }

        public int GetEntryCount()
        {
            int count = 0;

            foreach (DirectoryEntry entry in ParentDirectory.Read())
            {
                bool isSplit = IsConcatenationFile(entry);

                if (CanReturnEntry(entry, isSplit)) count++;
            }

            return count;
        }

        private bool CanReturnEntry(DirectoryEntry entry, bool isSplit)
        {
            return Mode.HasFlag(OpenDirectoryMode.Files) && (entry.Type == DirectoryEntryType.File || isSplit) ||
                   Mode.HasFlag(OpenDirectoryMode.Directories) && entry.Type == DirectoryEntryType.Directory && !isSplit;
        }

        private bool IsConcatenationFile(DirectoryEntry entry)
        {
#if CROSS_PLATFORM
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ConcatenationFileSystem.HasConcatenationFileAttribute(entry.Attributes);
            }
            else
            {
                return ParentFileSystem.IsConcatenationFile(entry.FullPath);
            }
#else
            return ConcatenationFileSystem.HasConcatenationFileAttribute(entry.Attributes);
#endif
        }
    }
}
