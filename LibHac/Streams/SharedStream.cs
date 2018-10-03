using System;
using System.IO;

namespace LibHac.Streams
{
    public class SharedStream : Stream
    {
        private readonly SharedStreamSource _stream;
        private readonly long _offset;
        private long _position;

        public SharedStream(SharedStreamSource source, long offset, long length)
        {
            _stream = source;
            _offset = offset;
            Length = length;
        }

        public override void Flush() => _stream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = Length - Position;
            if (remaining <= 0) return 0;
            if (remaining < count) count = (int)remaining;

            int bytesRead = _stream.Read(_offset + _position, buffer, offset, count);
            _position += bytesRead;
            return bytesRead;
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

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(_offset + _position, buffer, offset, count);
            _position += count;
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length { get; }

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _position = value;
            }
        }
    }
}
