using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace LibHac.Fs
{
    public abstract class StorageBase : IStorage
    {
        // 0 = not disposed; 1 = disposed
        private int _disposedState;
        private bool IsDisposed => _disposedState != 0;

        protected abstract Result ReadImpl(long offset, Span<byte> destination);
        protected abstract Result WriteImpl(long offset, ReadOnlySpan<byte> source);
        protected abstract Result FlushImpl();
        protected abstract Result GetSizeImpl(out long size);
        protected abstract Result SetSizeImpl(long size);

        protected virtual Result OperateRangeImpl(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            return ResultFs.NotImplemented.Log();
        }

        public Result Read(long offset, Span<byte> destination)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return ReadImpl(offset, destination);
        }

        public Result Write(long offset, ReadOnlySpan<byte> source)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return WriteImpl(offset, source);
        }

        public Result Flush()
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return FlushImpl();
        }

        public Result SetSize(long size)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return SetSizeImpl(size);
        }

        public Result GetSize(out long size)
        {
            size = default;
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return GetSizeImpl(out size);
        }

        public Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return OperateRange(outBuffer, operationId, offset, size, inBuffer);
        }

        public void Dispose()
        {
            // Make sure Dispose is only called once
            if (Interlocked.CompareExchange(ref _disposedState, 1, 0) == 0)
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        protected virtual void Dispose(bool disposing) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRangeValid(long offset, long size, long totalSize)
        {
            return offset >= 0 && size >= 0 && size <= totalSize && offset <= totalSize - size;
        }
    }
}
