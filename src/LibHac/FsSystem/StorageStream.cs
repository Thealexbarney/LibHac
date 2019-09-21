using System;
using System.IO;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class StorageStream : Stream
    {
        private IStorage BaseStorage { get; }
        private bool LeaveOpen { get; }
        private long _length;

        public StorageStream(IStorage baseStorage, FileAccess access, bool leaveOpen)
        {
            BaseStorage = baseStorage;
            LeaveOpen = leaveOpen;

            baseStorage.GetSize(out _length).ThrowIfFailure();

            CanRead = access.HasFlag(FileAccess.Read);
            CanWrite = access.HasFlag(FileAccess.Write);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int toRead = (int)Math.Min(count, Length - Position);
            BaseStorage.Read(Position, buffer.AsSpan(offset, toRead)).ThrowIfFailure();

            Position += toRead;
            return toRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            BaseStorage.Write(Position, buffer.AsSpan(offset, count)).ThrowIfFailure();
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
            BaseStorage.SetSize(value).ThrowIfFailure();

            BaseStorage.GetSize(out _length).ThrowIfFailure();
        }

        public override bool CanRead { get; }
        public override bool CanSeek => true;
        public override bool CanWrite { get; }
        public override long Length => _length;
        public override long Position { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (!LeaveOpen) BaseStorage?.Dispose();
            base.Dispose(disposing);
        }
    }
}
