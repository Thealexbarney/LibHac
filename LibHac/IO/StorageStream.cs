﻿using System;
using System.IO;

namespace LibHac.IO
{
    public class StorageStream : Stream
    {
        private Storage BaseStorage { get; }
        private bool LeaveOpen { get; }

        public StorageStream(Storage baseStorage, bool leaveOpen)
        {
            BaseStorage = baseStorage;
            LeaveOpen = leaveOpen;
            Length = baseStorage.Length;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int toRead = (int) Math.Min(count, Length - Position);
            int bytesRead = BaseStorage.Read(buffer, Position, toRead, offset);

            Position += bytesRead;
            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            BaseStorage.Write(buffer, Position, count, offset);
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

        public override bool CanRead => BaseStorage.CanRead;
        public override bool CanSeek => true;
        public override bool CanWrite => BaseStorage.CanWrite;
        public override long Length { get; }
        public override long Position { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (!LeaveOpen) BaseStorage?.Dispose();
            base.Dispose(disposing);
        }
    }
}
