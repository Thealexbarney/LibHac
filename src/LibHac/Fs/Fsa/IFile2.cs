using System;
using LibHac.Diag;

namespace LibHac.Fs.Fsa
{
    // ReSharper disable once InconsistentNaming
    public abstract class IFile2 : IDisposable
    {
        public Result Read(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
        {
            if (destination.IsEmpty)
            {
                bytesRead = 0;
                return Result.Success;
            }

            if (offset < 0)
            {
                bytesRead = 0;
                return ResultFs.OutOfRange.Log();
            }

            if (long.MaxValue - offset < destination.Length)
            {
                bytesRead = 0;
                return ResultFs.OutOfRange.Log();
            }

            return DoRead(out bytesRead, offset, destination, in option);
        }

        public Result Read(out long bytesRead, long offset, Span<byte> destination)
        {
            return Read(out bytesRead, offset, destination, ReadOption.None);
        }

        public Result Write(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            if (source.IsEmpty)
            {
                if (option.HasFlushFlag())
                {
                    Result rc = Flush();
                    if (rc.IsFailure()) return rc;
                }

                return Result.Success;
            }

            if (offset < 0)
                return ResultFs.OutOfRange.Log();

            if (long.MaxValue - offset < source.Length)
                return ResultFs.OutOfRange.Log();

            return DoWrite(offset, source, in option);
        }

        public Result Flush()
        {
            return DoFlush();
        }

        public Result SetSize(long size)
        {
            if (size < 0)
                return ResultFs.OutOfRange.Log();

            return DoSetSize(size);
        }

        public Result GetSize(out long size)
        {
            return DoGetSize(out size);
        }

        public Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            return DoOperateRange(outBuffer, operationId, offset, size, inBuffer);
        }

        public Result OperateRange(OperationId operationId, long offset, long size)
        {
            return DoOperateRange(Span<byte>.Empty, operationId, offset, size, ReadOnlySpan<byte>.Empty);
        }

        protected Result DryRead(out long readableBytes, long offset, long size, in ReadOption option,
            OpenMode openMode)
        {
            // Check that we can read.
            if (!openMode.HasFlag(OpenMode.Read))
            {
                readableBytes = default;
                return ResultFs.InvalidOpenModeForRead.Log();
            }

            // Get the file size, and validate our offset.
            Result rc = GetSize(out long fileSize);
            if (rc.IsFailure())
            {
                readableBytes = default;
                return rc;
            }

            if (offset > fileSize)
            {
                readableBytes = default;
                return ResultFs.OutOfRange.Log();
            }

            readableBytes = Math.Min(fileSize - offset, size);
            return Result.Success;
        }

        protected Result DrySetSize(long size, OpenMode openMode)
        {
            // Check that we can write.
            if (!openMode.HasFlag(OpenMode.Write))
                return ResultFs.InvalidOpenModeForWrite.Log();

            Assert.AssertTrue(size >= 0);

            return Result.Success;
        }

        protected Result DryWrite(out bool needsAppend, long offset, long size, in WriteOption option,
            OpenMode openMode)
        {
            // Check that we can write.
            if (!openMode.HasFlag(OpenMode.Write))
            {
                needsAppend = default;
                return ResultFs.InvalidOpenModeForWrite.Log();
            }

            // Get the file size.
            Result rc = GetSize(out long fileSize);
            if (rc.IsFailure())
            {
                needsAppend = default;
                return rc;
            }

            if (fileSize < offset + size)
            {
                if (!openMode.HasFlag(OpenMode.AllowAppend))
                {
                    needsAppend = default;
                    return ResultFs.FileExtensionWithoutOpenModeAllowAppend.Log();
                }

                needsAppend = true;
            }
            else
            {
                needsAppend = false;
            }

            return Result.Success;
        }

        protected abstract Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option);
        protected abstract Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option);
        protected abstract Result DoFlush();
        protected abstract Result DoSetSize(long size);
        protected abstract Result DoGetSize(out long size);
        protected abstract Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer);

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
