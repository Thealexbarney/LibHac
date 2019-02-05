using System.Collections.Generic;
using DiscUtils;
using LibHac.IO;
using DirectoryEntry = LibHac.IO.DirectoryEntry;
using IFileSystem = LibHac.IO.IFileSystem;

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
            if (Mode.HasFlag(OpenDirectoryMode.Directories))
            {
                foreach (DiscDirectoryInfo dir in DirInfo.GetDirectories())
                {
                    yield return new DirectoryEntry(dir.Name, FullPath + '/' + dir.Name, DirectoryEntryType.Directory, 0);
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.Files))
            {
                foreach (DiscFileInfo file in DirInfo.GetFiles())
                {
                    yield return new DirectoryEntry(file.Name, FullPath + '/' + file.Name, DirectoryEntryType.File, file.Length);
                }
            }
        }

        public int GetEntryCount()
        {
            int count = 0;

            if (Mode.HasFlag(OpenDirectoryMode.Directories))
            {
                count += DirInfo.GetDirectories().Length;
            }

            if (Mode.HasFlag(OpenDirectoryMode.Files))
            {
                count += DirInfo.GetFiles().Length;
            }

            return count;
        }
    }
}
