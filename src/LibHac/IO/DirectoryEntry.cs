namespace LibHac.IO
{
    public class DirectoryEntry
    {
        public string Name { get; }
        public string FullPath { get; }
        public DirectoryEntryType Type { get; }
        public long Size { get; }

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
        File
    }
}
