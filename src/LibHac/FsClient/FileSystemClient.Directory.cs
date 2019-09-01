using System;
using System.Collections.Generic;
using LibHac.Fs;

namespace LibHac.FsClient
{
    public partial class FileSystemClient
    {
        public Result GetDirectoryEntryCount(out long count, DirectoryHandle handle)
        {
            throw new NotImplementedException();
        }

        // todo: change to not use IEnumerable
        public IEnumerable<DirectoryEntry> ReadDirectory(DirectoryHandle handle)
        {
            throw new NotImplementedException();
        }

        public void CloseDirectory(DirectoryHandle handle)
        {
            throw new NotImplementedException();
        }
    }
}
