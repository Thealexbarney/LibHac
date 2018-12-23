using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac.IO
{
    public abstract class Storage : IStorage
    {
        private bool _isDisposed;
        protected internal List<IDisposable> ToDispose { get; } = new List<IDisposable>();

        protected abstract void ReadImpl(Span<byte> destination, long offset);
        protected abstract void WriteImpl(ReadOnlySpan<byte> source, long offset);
        public abstract void Flush();
        public abstract long Length { get; }

        protected FileAccess Access { get; set; } = FileAccess.ReadWrite;

        public void Read(Span<byte> destination, long offset)
        {
            EnsureCanRead();
            ValidateSpanParameters(destination, offset);
            ReadImpl(destination, offset);
        }

        public virtual void Read(byte[] buffer, long offset, int count, int bufferOffset)
        {
            ValidateArrayParameters(buffer, offset, count, bufferOffset);
            Read(buffer.AsSpan(bufferOffset, count), offset);
        }

        public void Write(ReadOnlySpan<byte> source, long offset)
        {
            EnsureCanWrite();
            ValidateSpanParameters(source, offset);
            WriteImpl(source, offset);
        }

        public virtual void Write(byte[] buffer, long offset, int count, int bufferOffset)
        {
            ValidateArrayParameters(buffer, offset, count, bufferOffset);
            Write(buffer.AsSpan(bufferOffset, count), offset);
        }

        public virtual Storage Slice(long start, long length, bool leaveOpen)
        {
            return new SubStorage(this, start, length, leaveOpen);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                Flush();
                foreach (IDisposable item in ToDispose)
                {
                    item?.Dispose();
                }
            }

            _isDisposed = true;
        }

        public void SetReadOnly() => Access = FileAccess.Read;

        public virtual bool CanRead => (Access & FileAccess.Read) != 0;
        public virtual bool CanWrite => (Access & FileAccess.Write) != 0;

        private void EnsureCanRead()
        {
            if (!CanRead) throw new InvalidOperationException("Storage is not readable");
        }

        private void EnsureCanWrite()
        {
            if (!CanWrite) throw new InvalidOperationException("Storage is not writable");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void ValidateArrayParameters(byte[] buffer, long offset, int count, int bufferOffset)
        {
            if (_isDisposed) throw new ObjectDisposedException(null);
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Argument must be non-negative.");
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Argument must be non-negative.");
            if (bufferOffset < 0) throw new ArgumentOutOfRangeException(nameof(bufferOffset), "Argument must be non-negative.");
            if (buffer.Length - bufferOffset < count) throw new ArgumentException("bufferOffset, length, and count were out of bounds for the array.");

            if (Length != -1)
            {
                if (offset + count > Length) throw new ArgumentException("The given offset and count exceed the length of the Storage");
            }
        }

        protected void ValidateSpanParameters(ReadOnlySpan<byte> destination, long offset)
        {
            if (_isDisposed) throw new ObjectDisposedException(null);
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Argument must be non-negative.");

            if (Length != -1)
            {
                if (offset + destination.Length > Length) throw new ArgumentException("The given offset and count exceed the length of the Storage");
            }
        }
    }
}
