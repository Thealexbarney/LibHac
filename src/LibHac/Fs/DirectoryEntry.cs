using System;

namespace LibHac.Fs
{
    public class DirectoryEntry
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public NxFileAttributes Attributes { get; set; }
        public DirectoryEntryType Type { get; set; }
        public long Size { get; set; }

        public DirectoryEntry(string name, string fullPath, DirectoryEntryType type, long size)
        {
            Name = name;
            FullPath = PathTools.Normalize(fullPath);
            Type = type;
            Size = size;
        }
    }

    public enum DirectoryEntryType
    {
        Directory,
        File,
        NotFound
    }

    [Flags]
    public enum NxFileAttributes
    {
        None = 0,
        Directory = 1 << 0,
        Archive = 1 << 1
    }
}
