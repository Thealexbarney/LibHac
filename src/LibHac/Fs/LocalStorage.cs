using System;
using System.IO;

namespace LibHac.Fs
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

            ToDispose.Add(Storage);
            ToDispose.Add(Stream);
        }

        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            return Storage.Read(offset, destination);
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            return Storage.Write(offset, source);
        }

        public override Result Flush()
        {
            return Storage.Flush();
        }

        public override Result GetSize(out long size)
        {
            return Storage.GetSize(out size);
        }
    }
}
