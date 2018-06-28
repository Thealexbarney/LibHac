using System;
using System.IO;

namespace libhac
{
    public class SubStream : Stream
    {
        private Stream BaseStream { get; }
        private long Offset { get; }

        public SubStream(Stream baseStream, long offset, long length)
        {
            if (baseStream == null) throw new ArgumentNullException(nameof(baseStream));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (!baseStream.CanSeek || !baseStream.CanRead) throw new NotSupportedException();

            BaseStream = baseStream;
            Length = length;
            Offset = offset;

            baseStream.Seek(offset, SeekOrigin.Current);
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = Length - Position;
            if (remaining <= 0) return 0;
            if (remaining < count) count = (int)remaining;
            return BaseStream.Read(buffer, offset, count);
        }

        public override long Length { get; }
        public override bool CanRead => BaseStream.CanRead;
        public override bool CanWrite => BaseStream.CanWrite;
        public override bool CanSeek => BaseStream.CanSeek;

        public override long Position
        {
            get => BaseStream.Position - Offset;
            set
            {
                if (value < 0 || value >= Length)
                    throw new ArgumentOutOfRangeException(nameof(value));

                BaseStream.Position = Offset + value;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = Length - offset;
                    break;
            }

            return Position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Flush() => BaseStream.Flush();

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
