using System;
using System.Collections.Generic;

namespace LibHac.Fs
{
    public abstract class StorageBase : IStorage
    {
        private bool _isDisposed;
        protected internal List<IDisposable> ToDispose { get; } = new List<IDisposable>();
        protected bool CanAutoExpand { get; set; }

        protected abstract Result ReadImpl(long offset, Span<byte> destination);
        protected abstract Result WriteImpl(long offset, ReadOnlySpan<byte> source);
        public abstract Result Flush();
        public abstract Result GetSize(out long size);

        public Result Read(long offset, Span<byte> destination)
        {
            ValidateParameters(destination, offset);
            return ReadImpl(offset, destination);
        }

        public Result Write(long offset, ReadOnlySpan<byte> source)
        {
            ValidateParameters(source, offset);
            return WriteImpl(offset, source);
        }

        public virtual Result SetSize(long size)
        {
            return ResultFs.NotImplemented.Log();
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void ValidateParameters(ReadOnlySpan<byte> span, long offset)
        {
            if (_isDisposed) throw new ObjectDisposedException(null);
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Argument must be non-negative.");

            Result sizeResult = GetSize(out long length);
            sizeResult.ThrowIfFailure();

            if (length != -1 && !CanAutoExpand)
            {
                if (offset + span.Length > length) throw new ArgumentException("The given offset and count exceed the length of the Storage");
            }
        }
    }
}
