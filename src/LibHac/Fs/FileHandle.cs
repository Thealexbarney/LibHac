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
            File.Parent.FsClient.CloseFile(this);
        }
    }

    public readonly struct FileHandle2
    {
        internal readonly Impl.FileAccessor File;

        internal FileHandle2(Impl.FileAccessor file)
        {
            File = file;
        }
    }
}
