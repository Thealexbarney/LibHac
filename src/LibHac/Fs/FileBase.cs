using System;
using System.Threading;

namespace LibHac.Fs
{
    public abstract class FileBase : IFile
    {
        // 0 = not disposed; 1 = disposed
        private int _disposedState;
        private bool IsDisposed => _disposedState != 0;

        protected abstract Result ReadImpl(out long bytesRead, long offset, Span<byte> destination, ReadOptionFlag options);
        protected abstract Result WriteImpl(long offset, ReadOnlySpan<byte> source, WriteOptionFlag options);
        protected abstract Result FlushImpl();
        protected abstract Result SetSizeImpl(long size);
        protected abstract Result GetSizeImpl(out long size);

        protected virtual Result OperateRangeImpl(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            return ResultFs.NotImplemented.Log();
        }

        public Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOptionFlag options)
        {
            bytesRead = default;

            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            if (destination.Length == 0) return Result.Success;
            if (offset < 0) return ResultFs.OutOfRange.Log();
            if (long.MaxValue - offset < destination.Length) return ResultFs.OutOfRange.Log();

            return ReadImpl(out bytesRead, offset, destination, options);
        }

        public Result Write(long offset, ReadOnlySpan<byte> source, WriteOptionFlag options)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            if (source.Length == 0)
            {
                if (options.HasFlag(WriteOptionFlag.Flush))
                {
                    return FlushImpl();
                }

                return Result.Success;
            }

            if (offset < 0) return ResultFs.OutOfRange.Log();
            if (long.MaxValue - offset < source.Length) return ResultFs.OutOfRange.Log();

            return WriteImpl(offset, source, options);
        }

        public Result Flush()
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return FlushImpl();
        }

        public Result SetSize(long size)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();
            if (size < 0) return ResultFs.OutOfRange.Log();

            return SetSizeImpl(size);
        }

        public Result GetSize(out long size)
        {
            if (IsDisposed)
            {
                size = default;
                return ResultFs.PreconditionViolation.Log();
            }

            return GetSizeImpl(out size);
        }

        public Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return OperateRangeImpl(outBuffer, operationId, offset, size, inBuffer);
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

        protected Result ValidateReadParams(out long bytesToRead, long offset, int size, OpenMode openMode)
        {
            bytesToRead = default;

            if (!openMode.HasFlag(OpenMode.Read))
            {
                return ResultFs.InvalidOpenModeForRead.Log();
            }

            Result rc = GetSize(out long fileSize);
            if (rc.IsFailure()) return rc;

            if (offset > fileSize)
            {
                return ResultFs.OutOfRange.Log();
            }

            bytesToRead = Math.Min(fileSize - offset, size);

            return Result.Success;
        }

        protected Result ValidateWriteParams(long offset, int size, OpenMode openMode, out bool isResizeNeeded)
        {
            isResizeNeeded = false;

            if (!openMode.HasFlag(OpenMode.Write))
            {
                return ResultFs.InvalidOpenModeForWrite.Log();
            }

            Result rc = GetSize(out long fileSize);
            if (rc.IsFailure()) return rc;

            if (offset + size > fileSize)
            {
                isResizeNeeded = true;

                if (!openMode.HasFlag(OpenMode.AllowAppend))
                {
                    return ResultFs.FileExtensionWithoutOpenModeAllowAppend.Log();
                }
            }

            return Result.Success;
        }
    }
}
