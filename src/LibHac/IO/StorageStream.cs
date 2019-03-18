using System;
using System.IO;

namespace LibHac.IO
{
    public class StorageStream : Stream
    {
        private IStorage BaseStorage { get; }
        private bool LeaveOpen { get; }

        public StorageStream(IStorage baseStorage, FileAccess access, bool leaveOpen)
        {
            BaseStorage = baseStorage;
            LeaveOpen = leaveOpen;
            Length = baseStorage.GetSize();

            CanRead = access.HasFlag(FileAccess.Read);
            CanWrite = access.HasFlag(FileAccess.Write);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int toRead = (int) Math.Min(count, Length - Position);
            BaseStorage.Read(buffer.AsSpan(offset, toRead), Position);

            Position += toRead;
            return toRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            BaseStorage.Write(buffer.AsSpan(offset, count), Position);
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

        public override bool CanRead { get; }
        public override bool CanSeek => true;
        public override bool CanWrite { get; }
        public override long Length { get; }
        public override long Position { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (!LeaveOpen) BaseStorage?.Dispose();
            base.Dispose(disposing);
        }
    }
}
