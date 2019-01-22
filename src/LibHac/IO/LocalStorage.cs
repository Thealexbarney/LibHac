using System;
using System.IO;

namespace LibHac.IO
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

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            Storage.Read(destination, offset);
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            Storage.Write(source, offset);
        }

        public override void Flush()
        {
            Storage.Flush();
        }

        public override long Length => Stream.Length;
    }
}
