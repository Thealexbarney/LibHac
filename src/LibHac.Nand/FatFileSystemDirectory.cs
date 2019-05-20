using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using DiscUtils;
using LibHac.Fs;

using DirectoryEntry = LibHac.Fs.DirectoryEntry;
using IFileSystem = LibHac.Fs.IFileSystem;

namespace LibHac.Nand
{
    public class FatFileSystemDirectory : IDirectory
    {
        public IFileSystem ParentFileSystem { get; }
        public string FullPath { get; }
        public OpenDirectoryMode Mode { get; }
        private DiscDirectoryInfo DirInfo { get; }

        public FatFileSystemDirectory(FatFileSystemProvider fs, string path, OpenDirectoryMode mode)
        {
            ParentFileSystem = fs;
            FullPath = path;
            Mode = mode;

            path = FatFileSystemProvider.ToDiscUtilsPath(PathTools.Normalize(path));

            DirInfo = fs.Fs.GetDirectoryInfo(path);
        }

        public IEnumerable<DirectoryEntry> Read()
        {
            foreach (DiscFileSystemInfo entry in DirInfo.GetFileSystemInfos())
            {
                bool isDir = (entry.Attributes & FileAttributes.Directory) != 0;

                if (!CanReturnEntry(isDir, Mode)) continue;

                DirectoryEntryType type = isDir ? DirectoryEntryType.Directory : DirectoryEntryType.File;
                long length = isDir ? 0 : entry.FileSystem.GetFileLength(entry.FullName);

                yield return new DirectoryEntry(entry.Name, PathTools.Combine(FullPath, entry.Name), type, length)
                {
                    Attributes = entry.Attributes.ToNxAttributes()
                };
            }
        }

        public int GetEntryCount()
        {
            int count = 0;

            foreach (DiscFileSystemInfo entry in DirInfo.GetFileSystemInfos())
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
