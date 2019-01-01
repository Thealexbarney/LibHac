using System.Collections.Generic;
using System.IO;

namespace LibHac.IO
{
    public class RomFsDirectory : IDirectory
    {
        public IFileSystem ParentFileSystem { get; }
        public string FullPath { get; }

        private RomfsDir Directory { get; }
        private OpenDirectoryMode Mode { get; }

        public RomFsDirectory(RomFsFileSystem fs, string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            if (!fs.DirectoryDict.TryGetValue(path, out RomfsDir dir))
            {
                throw new DirectoryNotFoundException(path);
            }

            ParentFileSystem = fs;
            Directory = dir;
            FullPath = path;
            Mode = mode;
        }

        public IEnumerable<DirectoryEntry> Read()
        {
            if (Mode.HasFlag(OpenDirectoryMode.Directories))
            {
                RomfsDir dirEntry = Directory.FirstChild;

                while (dirEntry != null)
                {
                    yield return new DirectoryEntry(dirEntry.Name, FullPath + '/' + dirEntry.Name, DirectoryEntryType.Directory, 0);
                    dirEntry = dirEntry.NextSibling;
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.Files))
            {
                RomfsFile fileEntry = Directory.FirstFile;

                while (fileEntry != null)
                {
                    yield return new DirectoryEntry(fileEntry.Name, FullPath + '/' + fileEntry.Name, DirectoryEntryType.File, fileEntry.DataLength);
                    fileEntry = fileEntry.NextSibling;
                }
            }
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
