using System.Collections.Generic;

namespace LibHac.Fs.RomFs
{
    public class RomFsDirectory : IDirectory
    {
        IFileSystem IDirectory.ParentFileSystem => ParentFileSystem;
        public RomFsFileSystem ParentFileSystem { get; }
        public string FullPath { get; }

        public OpenDirectoryMode Mode { get; }

        private FindPosition InitialPosition { get; }

        public RomFsDirectory(RomFsFileSystem fs, string path, FindPosition position, OpenDirectoryMode mode)
        {
            ParentFileSystem = fs;
            InitialPosition = position;
            FullPath = path;
            Mode = mode;
        }

        public IEnumerable<DirectoryEntry> Read()
        {
            FindPosition position = InitialPosition;
            HierarchicalRomFileTable<RomFileInfo> tab = ParentFileSystem.FileTable;

            if (Mode.HasFlag(OpenDirectoryMode.Directory))
            {
                while (tab.FindNextDirectory(ref position, out string name))
                {
                    yield return new DirectoryEntry(name, PathTools.Combine(FullPath, name), DirectoryEntryType.Directory, 0);
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.File))
            {
                while (tab.FindNextFile(ref position, out RomFileInfo info, out string name))
                {
                    yield return new DirectoryEntry(name, PathTools.Combine(FullPath, name), DirectoryEntryType.File, info.Length);
                }
            }
        }

        public int GetEntryCount()
        {
            int count = 0;

            FindPosition position = InitialPosition;
            HierarchicalRomFileTable<RomFileInfo> tab = ParentFileSystem.FileTable;

            if (Mode.HasFlag(OpenDirectoryMode.Directory))
            {
                while (tab.FindNextDirectory(ref position, out string _))
                {
                    count++;
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.File))
            {
                while (tab.FindNextFile(ref position, out RomFileInfo _, out string _))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
