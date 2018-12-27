using System.IO;

namespace LibHac.IO
{
    public class RomFsDirectory : IDirectory
    {
        public IFileSystem ParentFileSystem { get; }

        private RomfsDir Directory { get; }
        private OpenDirectoryMode Mode { get; }

        public RomFsDirectory(RomFsFileSystem fs, string path, OpenDirectoryMode mode)
        {
            if (!fs.DirectoryDict.TryGetValue(path, out RomfsDir dir))
            {
                throw new DirectoryNotFoundException(path);
            }

            ParentFileSystem = fs;
            Directory = dir;
            Mode = mode;
        }

        public DirectoryEntry[] Read()
        {
            int count = GetEntryCount();

            var entries = new DirectoryEntry[count];
            int index = 0;

            if (Mode.HasFlag(OpenDirectoryMode.Directories))
            {
                RomfsDir dirEntry = Directory.FirstChild;

                while (dirEntry != null)
                {
                    entries[index] = new DirectoryEntry(dirEntry.FullPath, DirectoryEntryType.Directory, 0);
                    dirEntry = dirEntry.NextSibling;
                    index++;
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.Files))
            {
                RomfsFile fileEntry = Directory.FirstFile;

                while (fileEntry != null)
                {
                    entries[index] =
                        new DirectoryEntry(fileEntry.FullPath, DirectoryEntryType.File, fileEntry.DataLength);
                    fileEntry = fileEntry.NextSibling;
                    index++;
                }
            }

            return entries;
        }

        public int GetEntryCount()
        {
            int count = 0;

            if (Mode.HasFlag(OpenDirectoryMode.Directories))
            {
                RomfsDir dirEntry = Directory.FirstChild;

                while (dirEntry != null)
                {
                    count++;
                    dirEntry = dirEntry.NextSibling;
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.Files))
            {
                RomfsFile fileEntry = Directory.FirstFile;

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
