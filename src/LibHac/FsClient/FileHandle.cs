using System;
using LibHac.FsClient.Accessors;

namespace LibHac.FsClient
{
    public struct FileHandle : IDisposable
    {
        internal readonly FileAccessor File;

        internal FileHandle(FileAccessor file)
        {
            File = file;
        }

        public int GetId() => File.GetHashCode();

        public void Dispose()
        {
            File.Parent.FsManager.CloseFile(this);
        }
    }
}
