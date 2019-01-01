using System;
using System.IO;

namespace LibHac.IO
{
    public class LocalFile : FileBase
    {
        private string Path { get; }
        private StreamStorage Storage { get; }

        public LocalFile(string path, OpenMode mode)
        {
            Path = path;
            Mode = mode;
            Storage = new StreamStorage(new FileStream(Path, FileMode.Open), false);
        }

        public override int Read(Span<byte> destination, long offset)
        {
            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            Storage.Read(destination.Slice(0, toRead), offset);

            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset)
        {
            ValidateWriteParams(source, offset);
            
            Storage.Write(source, offset);
        }

        public override void Flush()
        {
            Storage.Flush();
        }

        public override long GetSize()
        {
            return Storage.Length;
        }

        public override void SetSize(long size)
        {
            throw new NotImplementedException();
        }
    }
}
