using System.Collections.Generic;

namespace LibHac.IO.Save
{
    class SaveDataDirectory : IDirectory
    {
        public IFileSystem ParentFileSystem { get; }
        public string FullPath { get; }

        public OpenDirectoryMode Mode { get; }
        private SaveDirectoryEntry Directory { get; }

        public SaveDataDirectory(SaveDataFileSystemCore fs, string path, SaveDirectoryEntry dir, OpenDirectoryMode mode)
        {
            ParentFileSystem = fs;
            Directory = dir;
            FullPath = path;
            Mode = mode;
        }

        public IEnumerable<DirectoryEntry> Read()
        {
            if (Mode.HasFlag(OpenDirectoryMode.Directories))
            {
                SaveDirectoryEntry dirEntry = Directory.FirstChild;

                while (dirEntry != null)
                {
                    yield return new DirectoryEntry(dirEntry.Name, FullPath + '/' + dirEntry.Name, DirectoryEntryType.Directory, 0);
                    dirEntry = dirEntry.NextSibling;
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.Files))
            {
                SaveFileEntry fileEntry = Directory.FirstFile;

                while (fileEntry != null)
                {
                    yield return new DirectoryEntry(fileEntry.Name, FullPath + '/' + fileEntry.Name, DirectoryEntryType.File, fileEntry.FileSize);
                    fileEntry = fileEntry.NextSibling;
                }
            }
        }

        public int GetEntryCount()
        {
            int count = 0;

            if (Mode.HasFlag(OpenDirectoryMode.Directories))
            {
                SaveDirectoryEntry dirEntry = Directory.FirstChild;

                while (dirEntry != null)
                {
                    count++;
                    dirEntry = dirEntry.NextSibling;
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.Files))
            {
                SaveFileEntry fileEntry = Directory.FirstFile;

                while (fileEntry != null)
                {
                    count++;
                    fileEntry = fileEntry.NextSibling;
                }
            }

            return count;
        }
    }
}
