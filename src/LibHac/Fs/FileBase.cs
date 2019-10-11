using System;
using System.Threading;

namespace LibHac.Fs
{
    public abstract class FileBase : IFile
    {
        // 0 = not disposed; 1 = disposed
        private int _disposedState;
        private bool IsDisposed => _disposedState != 0;

        public abstract Result ReadImpl(out long bytesRead, long offset, Span<byte> destination, ReadOption options);
        public abstract Result WriteImpl(long offset, ReadOnlySpan<byte> source, WriteOption options);
        public abstract Result FlushImpl();
        public abstract Result GetSizeImpl(out long size);
        public abstract Result SetSizeImpl(long size);

        public Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            bytesRead = default;

            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            if (destination.Length == 0) return Result.Success;
            if (offset < 0) return ResultFs.ValueOutOfRange.Log();

            return ReadImpl(out bytesRead, offset, destination, options);
        }

        public Result Write(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            if (source.Length == 0)
            {
                if (options.HasFlag(WriteOption.Flush))
                {
                    return Flush();
                }

                return Result.Success;
            }

            if (offset < 0) return ResultFs.ValueOutOfRange.Log();

            return WriteImpl(offset, source, options);
        }

        public Result Flush()
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return FlushImpl();
        }

        public Result GetSize(out long size)
        {
            size = default;
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return GetSizeImpl(out size);
        }

        public Result SetSize(long size)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();
            if (size < 0) return ResultFs.ValueOutOfRange.Log();

            return SetSizeImpl(size);
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
                return ResultFs.ValueOutOfRange.Log();
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
