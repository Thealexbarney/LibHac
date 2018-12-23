namespace LibHac.IO
{
    public class DirectoryEntry
    {
        public string Name { get; }
        public DirectoryEntryType Type { get; }
        public long Size { get; }

        public DirectoryEntry(string name, DirectoryEntryType type, long size)
        {
            Name = name;
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
