namespace LibHac.IO
{
    public interface IDirectory
    {
        IFileSystem ParentFileSystem { get; }

        DirectoryEntry[] Read();
        int GetEntryCount();
    }
}