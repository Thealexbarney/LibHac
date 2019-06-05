using System;

namespace LibHac.Fs
{
    public class NullFile : FileBase
    {
        public NullFile()
        {
            Mode = OpenMode.ReadWrite;
        }

        public NullFile(long length) : this() => Length = length;

        private long Length { get; }

        public override int Read(Span<byte> destination, long offset, ReadOption options)
        {
            int toRead = ValidateReadParamsAndGetSize(destination, offset);
            destination.Slice(0, toRead).Clear();
            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset, WriteOption options)
        {
        }

        public override void Flush()
        {
        }

        public override long GetSize() => Length;

        public override void SetSize(long size)
        {
            throw new NotSupportedException();
        }
    }
}
