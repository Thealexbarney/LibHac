using System;
using System.IO;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class LocalStorage : StorageBase
    {
        private string Path { get; }
        private FileStream Stream { get; }
        private StreamStorage Storage { get; }

        public LocalStorage(string path, FileAccess access) : this(path, access, FileMode.Open) { }

        public LocalStorage(string path, FileAccess access, FileMode mode)
        {
            Path = path;
            Stream = new FileStream(Path, mode, access);
            Storage = new StreamStorage(Stream, false);
        }

        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            return Storage.Read(offset, destination);
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            return Storage.Write(offset, source);
        }

        protected override Result FlushImpl()
        {
            return Storage.Flush();
        }

        protected override Result SetSizeImpl(long size)
        {
            return ResultFs.NotImplemented.Log();
        }

        protected override Result GetSizeImpl(out long size)
        {
            return Storage.GetSize(out size);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Storage?.Dispose();
                Stream?.Dispose();
            }
        }
    }
}
