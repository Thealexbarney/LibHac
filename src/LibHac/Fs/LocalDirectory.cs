using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace LibHac.Fs
{
    public class LocalDirectory : IDirectory
    {
        public IFileSystem ParentFileSystem { get; }
        public string FullPath { get; }

        private string LocalPath { get; }
        public OpenDirectoryMode Mode { get; }
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
            foreach (FileSystemInfo entry in DirInfo.EnumerateFileSystemInfos())
            {
                bool isDir = (entry.Attributes & FileAttributes.Directory) != 0;

                if (!CanReturnEntry(isDir, Mode)) continue;

                DirectoryEntryType type = isDir ? DirectoryEntryType.Directory : DirectoryEntryType.File;
                long length = isDir ? 0 : ((FileInfo)entry).Length;

                yield return new DirectoryEntry(entry.Name, PathTools.Combine(FullPath, entry.Name), type, length)
                {
                    Attributes = entry.Attributes.ToNxAttributes()
                };
            }
        }

        public int GetEntryCount()
        {
            int count = 0;

            foreach (FileSystemInfo entry in DirInfo.EnumerateFileSystemInfos())
            {
                bool isDir = (entry.Attributes & FileAttributes.Directory) != 0;

                if (CanReturnEntry(isDir, Mode)) count++;
            }

            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanReturnEntry(bool isDir, OpenDirectoryMode mode)
        {
            return isDir && (mode & OpenDirectoryMode.Directories) != 0 ||
                   !isDir && (mode & OpenDirectoryMode.Files) != 0;
        }
    }
}
