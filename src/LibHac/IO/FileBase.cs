using System;
using System.Collections.Generic;

namespace LibHac.IO
{
    public abstract class FileBase : IFile
    {
        private bool _isDisposed;
        internal List<IDisposable> ToDispose { get; } = new List<IDisposable>();

        public abstract int Read(Span<byte> destination, long offset);
        public abstract void Write(ReadOnlySpan<byte> source, long offset);
        public abstract void Flush();
        public abstract long GetSize();
        public abstract void SetSize(long size);

        public OpenMode Mode { get; protected set; }

        protected int ValidateReadParamsAndGetSize(ReadOnlySpan<byte> span, long offset)
        {
            if (_isDisposed) throw new ObjectDisposedException(null);

            if ((Mode & OpenMode.Read) == 0) throw new NotSupportedException("File does not allow reading.");
            if (span == null) throw new ArgumentNullException(nameof(span));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Argument must be non-negative.");

            long fileSize = GetSize();
            int size = span.Length;

            if (offset > fileSize) throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be less than the file size.");

            return (int)Math.Min(fileSize - offset, size);
        }

        protected void ValidateWriteParams(ReadOnlySpan<byte> span, long offset)
        {
            if (_isDisposed) throw new ObjectDisposedException(null);

            if ((Mode & OpenMode.Write) == 0) throw new NotSupportedException("File does not allow writing.");

            if (span == null) throw new ArgumentNullException(nameof(span));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Argument must be non-negative.");

            long fileSize = GetSize();
            int size = span.Length;

            if (offset + size > fileSize)
            {
                if ((Mode & OpenMode.Append) == 0)
                {
                    throw new NotSupportedException("File does not allow appending.");
                }

                SetSize(offset + size);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
    }

    /// <summary>
    /// Specifies which operations are available on an <see cref="IFile"/>.
    /// </summary>
    [Flags]
    public enum OpenMode
    {
        Read = 1,
        Write = 2,
        Append = 4,
        ReadWrite = Read | Write
    }
}
