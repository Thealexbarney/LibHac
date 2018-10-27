using System;
using System.Collections.Generic;
using System.IO;
using LibHac.IO;

namespace LibHac
{
    public abstract class Storage : IDisposable
    {
        private bool _isDisposed;
        protected List<IDisposable> ToDispose { get; } = new List<IDisposable>();

        protected abstract int ReadSpan(Span<byte> destination, long offset);
        protected abstract void WriteSpan(ReadOnlySpan<byte> source, long offset);
        public abstract void Flush();
        public abstract long Length { get; }

        public int Read(Span<byte> destination, long offset)
        {
            ValidateSpanParameters(destination, offset);
            return ReadSpan(destination, offset);
        }

        public virtual int Read(byte[] buffer, long offset, int count, int bufferOffset)
        {
            ValidateArrayParameters(buffer, offset, count, bufferOffset);
            return Read(buffer.AsSpan(bufferOffset, count), offset);
        }

        public void Write(ReadOnlySpan<byte> source, long offset)
        {
            ValidateSpanParameters(source, offset);
            WriteSpan(source, offset);
        }

        public virtual void Write(byte[] buffer, long offset, int count, int bufferOffset)
        {
            ValidateArrayParameters(buffer, offset, count, bufferOffset);
            Write(buffer.AsSpan(bufferOffset, count), offset);
        }

        public Stream AsStream() => new StorageStream(this, true);

        public SubStorage Slice(long start, long length)
        {
            return new SubStorage(this, start, length);
        }

        public SubStorage Slice(long start)
        {
            if (Length == -1)
            {
                throw new InvalidOperationException();
            }

            return Slice(start, Length - start);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                foreach (IDisposable item in ToDispose)
                {
                    item?.Dispose();
                }
            }

            _isDisposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void ValidateArrayParameters(byte[] buffer, long offset, int count, int bufferOffset)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Argument must be non-negative.");
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Argument must be non-negative.");
            if (bufferOffset < 0) throw new ArgumentOutOfRangeException(nameof(bufferOffset), "Argument must be non-negative.");
            if (buffer.Length - bufferOffset < count) throw new ArgumentException("bufferOffset, length, and count were out of bounds for the array.");

            if (Length != -1)
            {
                if (offset + count > Length) throw new ArgumentException();
            }
        }

        private void ValidateSpanParameters(ReadOnlySpan<byte> destination, long offset)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Argument must be non-negative.");

            if (Length != -1)
            {
                if (offset + destination.Length > Length) throw new ArgumentException("Storage");
            }
        }
    }
}
