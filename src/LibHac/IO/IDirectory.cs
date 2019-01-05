using System.Collections.Generic;

namespace LibHac.IO
{
    public interface IDirectory
    {
        IFileSystem ParentFileSystem { get; }
        string FullPath { get; }
        OpenDirectoryMode Mode { get; }

        IEnumerable<DirectoryEntry> Read();
        int GetEntryCount();
    }
}