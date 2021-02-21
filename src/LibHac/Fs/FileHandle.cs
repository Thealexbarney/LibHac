namespace LibHac.Fs
{
    public readonly struct FileHandle
    {
        internal readonly Impl.FileAccessor File;

        public bool IsValid => File is not null;

        internal FileHandle(Impl.FileAccessor file)
        {
            File = file;
        }
    }
}
