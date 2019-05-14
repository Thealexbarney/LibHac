﻿using System;
using System.IO;

namespace LibHac.Fs
{
    public class NxFileStream : Stream
    {
        private IFile BaseFile { get; }
        private bool LeaveOpen { get; }
        private long _length;

        public NxFileStream(IFile baseFile, bool leaveOpen)
        {
            BaseFile = baseFile;
            LeaveOpen = leaveOpen;
            _length = baseFile.GetSize();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int toRead = (int)Math.Min(count, Length - Position);
            BaseFile.Read(buffer.AsSpan(offset, toRead), Position);

            Position += toRead;
            return toRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            BaseFile.Write(buffer.AsSpan(offset, count), Position);

            Position += count;
        }

        public override void Flush()
        {
            BaseFile.Flush();
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
            BaseFile.SetSize(value);

            _length = BaseFile.GetSize();
        }

        public override bool CanRead => BaseFile.Mode.HasFlag(OpenMode.Read);
        public override bool CanSeek => true;
        public override bool CanWrite => BaseFile.Mode.HasFlag(OpenMode.Write);
        public override long Length => _length;
        public override long Position { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (!LeaveOpen) BaseFile?.Dispose();
            base.Dispose(disposing);
        }
    }
}
