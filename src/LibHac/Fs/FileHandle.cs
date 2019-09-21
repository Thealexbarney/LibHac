using System;
using LibHac.Fs.Accessors;

namespace LibHac.Fs
{
    public struct FileHandle : IDisposable
    {
        internal readonly FileAccessor File;

        internal FileHandle(FileAccessor file)
        {
            File = file;
        }

        public int GetId() => File?.GetHashCode() ?? 0;

        public void Dispose()
        {
            File.Parent.FsManager.CloseFile(this);
        }
    }
}
