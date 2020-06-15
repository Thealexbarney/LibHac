using System;
using System.IO;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class NxFileStream : Stream
    {
        private IFile BaseFile { get; }
        private bool LeaveOpen { get; }
        private OpenMode Mode { get; }
        private long _length;

        public NxFileStream(IFile baseFile, bool leaveOpen) : this(baseFile, OpenMode.ReadWrite, leaveOpen) { }

        public NxFileStream(IFile baseFile, OpenMode mode, bool leaveOpen)
        {
            BaseFile = baseFile;
            Mode = mode;
            LeaveOpen = leaveOpen;

            baseFile.GetSize(out _length).ThrowIfFailure();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            BaseFile.Read(out long bytesRead, Position, buffer.AsSpan(offset, count));

            Position += bytesRead;
            return (int)bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            BaseFile.Write(Position, buffer.AsSpan(offset, count));

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
            BaseFile.SetSize(value).ThrowIfFailure();

            BaseFile.GetSize(out _length).ThrowIfFailure();
        }

        public override bool CanRead => Mode.HasFlag(OpenMode.Read);
        public override bool CanSeek => true;
        public override bool CanWrite => Mode.HasFlag(OpenMode.Write);
        public override long Length => _length;
        public override long Position { get; set; }

        protected override void Dispose(bool disposing)
        {
            if (!LeaveOpen) BaseFile?.Dispose();
            base.Dispose(disposing);
        }
    }
}
