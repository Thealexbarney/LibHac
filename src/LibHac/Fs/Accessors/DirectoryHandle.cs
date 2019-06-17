using System;

namespace LibHac.Fs.Accessors
{
    public struct DirectoryHandle : IDisposable
    {
        internal readonly DirectoryAccessor Directory;

        internal DirectoryHandle(DirectoryAccessor directory)
        {
            Directory = directory;
        }

        public void Dispose()
        {
            Directory.Dispose();
        }
    }
}
