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
    }
}
