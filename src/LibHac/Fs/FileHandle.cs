using System;
using LibHac.Fs.Fsa;

namespace LibHac.Fs
{
    public readonly struct FileHandle : IDisposable
    {
        internal readonly Impl.FileAccessor File;

        public bool IsValid => File is not null;

        internal FileHandle(Impl.FileAccessor file)
        {
            File = file;
        }

        public void Dispose()
        {
            if (IsValid)
            {
                File.FsClient.CloseFile(this);
            }
        }
    }
}
