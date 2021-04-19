using LibHac.Fs.Fsa;

namespace LibHac.Fs
{
    public readonly struct DirectoryHandle
    {
        internal readonly Impl.DirectoryAccessor Directory;

        public bool IsValid => Directory is not null;

        internal DirectoryHandle(Impl.DirectoryAccessor directory)
        {
            Directory = directory;
        }

        public void Dispose()
        {
            if (IsValid)
            {
                Directory.GetParent().FsClient.CloseDirectory(this);
            }
        }
    }
}