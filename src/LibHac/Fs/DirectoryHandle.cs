using System;
using LibHac.Fs.Accessors;

namespace LibHac.Fs
{
    public struct DirectoryHandle : IDisposable
    {
        internal readonly DirectoryAccessor Directory;

        internal DirectoryHandle(DirectoryAccessor directory)
        {
            Directory = directory;
        }

        public int GetId() => Directory?.GetHashCode() ?? 0;

        public void Dispose()
        {
            Directory.Parent.FsClient.CloseDirectory(this);
        }
    }

    public readonly struct DirectoryHandle2
    {
        internal readonly Impl.DirectoryAccessor Directory;

        internal DirectoryHandle2(Impl.DirectoryAccessor directory)
        {
            Directory = directory;
        }
    }
}
