using System;

namespace LibHac.Fs
{
    public partial class FileSystemClient
    {
        public Result GetDirectoryEntryCount(out long count, DirectoryHandle handle)
        {
            return FsManager.GetDirectoryEntryCount(out count, handle);
        }

        public Result ReadDirectory(out long entriesRead, Span<DirectoryEntry> entryBuffer, DirectoryHandle handle)
        {
            return FsManager.ReadDirectory(out entriesRead, entryBuffer, handle);
        }

        public void CloseDirectory(DirectoryHandle handle)
        {
            FsManager.CloseDirectory(handle);
        }
    }
}
