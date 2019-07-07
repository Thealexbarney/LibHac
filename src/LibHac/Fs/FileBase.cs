using System;
using System.Collections.Generic;

namespace LibHac.Fs
{
    public abstract class FileBase : IFile
    {
        protected bool IsDisposed { get; private set; }
        internal List<IDisposable> ToDispose { get; } = new List<IDisposable>();

        public abstract int Read(Span<byte> destination, long offset, ReadOption options);
        public abstract void Write(ReadOnlySpan<byte> source, long offset, WriteOption options);
        public abstract void Flush();
        public abstract long GetSize();
        public abstract void SetSize(long size);

        public OpenMode Mode { get; protected set; }

        protected int ValidateReadParamsAndGetSize(ReadOnlySpan<byte> span, long offset)
        {
            if (IsDisposed) throw new ObjectDisposedException(null);

            if ((Mode & OpenMode.Read) == 0) ThrowHelper.ThrowResult(ResultFs.InvalidOpenModeForRead, "File does not allow reading.");
            if (span == null) throw new ArgumentNullException(nameof(span));
            if (offset < 0) ThrowHelper.ThrowResult(ResultFs.ValueOutOfRange, "Offset must be non-negative.");

            long fileSize = GetSize();
            int size = span.Length;

            if (offset > fileSize) ThrowHelper.ThrowResult(ResultFs.ValueOutOfRange, "Offset must be less than the file size.");

            return (int)Math.Min(fileSize - offset, size);
        }

        protected void ValidateWriteParams(ReadOnlySpan<byte> span, long offset)
        {
            if (IsDisposed) throw new ObjectDisposedException(null);

            if ((Mode & OpenMode.Write) == 0) ThrowHelper.ThrowResult(ResultFs.InvalidOpenModeForWrite, "File does not allow writing.");

            if (span == null) throw new ArgumentNullException(nameof(span));
            if (offset < 0) ThrowHelper.ThrowResult(ResultFs.ValueOutOfRange, "Offset must be non-negative.");

            long fileSize = GetSize();
            int size = span.Length;

            if (offset + size > fileSize)
            {
                if ((Mode & OpenMode.Append) == 0)
                {
                    ThrowHelper.ThrowResult(ResultFs.AllowAppendRequiredForImplicitExtension);
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
            if (IsDisposed) return;

            if (disposing)
            {
                Flush();

                foreach (IDisposable item in ToDispose)
                {
                    item?.Dispose();
                }
            }

            IsDisposed = true;
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

    [Flags]
    public enum ReadOption
    {
        None = 0
    }

    [Flags]
    public enum WriteOption
    {
        None = 0,
        Flush = 1
    }
}
