using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LibHac.IO
{
    public class LocalDirectory : IDirectory
    {
        public IFileSystem ParentFileSystem { get; }
        public string FullPath { get; }

        private string LocalPath { get; }
        private OpenDirectoryMode Mode { get; }
        private DirectoryInfo DirInfo { get; }

        public LocalDirectory(LocalFileSystem fs, string path, OpenDirectoryMode mode)
        {
            ParentFileSystem = fs;
            FullPath = path;
            LocalPath = fs.ResolveLocalPath(path);
            Mode = mode;

            DirInfo = new DirectoryInfo(LocalPath);
        }

        public IEnumerable<DirectoryEntry> Read()
        {
            var entries = new List<DirectoryEntry>();

            if (Mode.HasFlag(OpenDirectoryMode.Directories))
            {
                foreach (DirectoryInfo dir in DirInfo.EnumerateDirectories())
                {
                    entries.Add(new DirectoryEntry(dir.Name, FullPath + '/' + dir.Name, DirectoryEntryType.Directory, 0));
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.Files))
            {
                foreach (FileInfo file in DirInfo.EnumerateFiles())
                {
                    entries.Add(new DirectoryEntry(file.Name, FullPath + '/' + file.Name, DirectoryEntryType.File, file.Length));
                }
            }

            return entries.ToArray();
        }

        public int GetEntryCount()
        {
            int count = 0;

            if (Mode.HasFlag(OpenDirectoryMode.Directories))
            {
                count += Directory.EnumerateDirectories(LocalPath).Count();
            }

            if (Mode.HasFlag(OpenDirectoryMode.Files))
            {
                count += Directory.EnumerateFiles(LocalPath).Count();
            }

            return count;
        }
    }
}
