using System.Collections.Generic;

namespace LibHac.Fs.Save
{
    public class SaveDataDirectory : IDirectory
    {
        IFileSystem IDirectory.ParentFileSystem => ParentFileSystem;
        public SaveDataFileSystemCore ParentFileSystem { get; }
        public string FullPath { get; }

        public OpenDirectoryMode Mode { get; }

        private SaveFindPosition InitialPosition { get; }

        public SaveDataDirectory(SaveDataFileSystemCore fs, string path, SaveFindPosition position, OpenDirectoryMode mode)
        {
            ParentFileSystem = fs;
            InitialPosition = position;
            FullPath = path;
            Mode = mode;
        }

        public IEnumerable<DirectoryEntry> Read()
        {
            SaveFindPosition position = InitialPosition;
            HierarchicalSaveFileTable tab = ParentFileSystem.FileTable;

            if (Mode.HasFlag(OpenDirectoryMode.Directory))
            {
                while (tab.FindNextDirectory(ref position, out string name))
                {
                    yield return new DirectoryEntry(name, PathTools.Combine(FullPath, name), DirectoryEntryType.Directory, 0);
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.File))
            {
                while (tab.FindNextFile(ref position, out SaveFileInfo info, out string name))
                {
                    yield return new DirectoryEntry(name, PathTools.Combine(FullPath, name), DirectoryEntryType.File, info.Length);
                }
            }
        }

        public int GetEntryCount()
        {
            int count = 0;

            SaveFindPosition position = InitialPosition;
            HierarchicalSaveFileTable tab = ParentFileSystem.FileTable;

            if (Mode.HasFlag(OpenDirectoryMode.Directory))
            {
                while (tab.FindNextDirectory(ref position, out string _))
                {
                    count++;
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.File))
            {
                while (tab.FindNextFile(ref position, out SaveFileInfo _, out string _))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
