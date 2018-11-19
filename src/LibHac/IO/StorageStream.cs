using System;
using System.IO;

namespace LibHac.IO
{
    public class StorageStream : Stream
    {
        private IStorage BaseStorage { get; }
        private bool LeaveOpen { get; }

        public StorageStream(IStorage baseStorage, bool leaveOpen)
        {
            BaseStorage = baseStorage;
            LeaveOpen = leaveOpen;
            Length = baseStorage.Length;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int toRead = (int) Math.Min(count, Length - Position);
            BaseStorage.Read(buffer, Position, toRead, offset);

            Position += toRead;
            return toRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            BaseStorage.Write(buffer, Position, count, offset);
            Position += count;
        }

        public override void Flush()
        {
            BaseStorage.Flush();
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

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length { get; }
        public override long Position { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (!LeaveOpen) BaseStorage?.Dispose();
            base.Dispose(disposing);
        }
    }
}
