using System;
using System.Runtime.CompilerServices;
using LibHac.Common;

namespace LibHac.Fs.Fsa
{
    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// Provides an interface for reading and writing a sequence of bytes.
    /// </summary>
    /// <remarks><see cref="IFile"/> is similar to <see cref="IStorage"/>, and has a few main differences:
    /// 
    /// - <see cref="IFile"/> allows an <see cref="OpenMode"/> to be set that controls read, write
    /// and append permissions for the file.
    ///
    /// - If the <see cref="IFile"/> cannot read or write as many bytes as requested, it will read
    /// or write as many bytes as it can and return that number of bytes to the caller.
    ///
    /// - If <see cref="Write"/> is called on an offset past the end of the <see cref="IFile"/>,
    /// the <see cref="OpenMode.AllowAppend"/> mode is set and the file supports expansion,
    /// the file will be expanded so that it is large enough to contain the written data.</remarks>
    public abstract class IFile : IDisposable
    {
        /// <summary>
        /// Reads a sequence of bytes from the current <see cref="IFile"/>.
        /// </summary>
        /// <param name="bytesRead">If the operation returns successfully, The total number of bytes read into
        /// the buffer. This can be less than the size of the buffer if the IFile is too short to fulfill the request.</param>
        /// <param name="offset">The offset in the <see cref="IFile"/> at which to begin reading.</param>
        /// <param name="destination">The buffer where the read bytes will be stored.
        /// The number of bytes read will be no larger than the length of the buffer.</param>
        /// <param name="option">Options for reading from the <see cref="IFile"/>.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        public Result Read(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
        {
            UnsafeHelpers.SkipParamInit(out bytesRead);

            if (Unsafe.IsNullRef(ref bytesRead))
                return ResultFs.NullptrArgument.Log();

            if (destination.IsEmpty)
            {
                bytesRead = 0;
                return Result.Success;
            }

            if (offset < 0)
                return ResultFs.OutOfRange.Log();

            if (long.MaxValue - offset < destination.Length)
                return ResultFs.OutOfRange.Log();

            return DoRead(out bytesRead, offset, destination, in option);
        }

        /// <summary>
        /// Reads a sequence of bytes from the current <see cref="IFile"/> with no <see cref="ReadOption"/>s.
        /// </summary>
        /// <param name="bytesRead">If the operation returns successfully, The total number of bytes read into
        /// the buffer. This can be less than the size of the buffer if the IFile is too short to fulfill the request.</param>
        /// <param name="offset">The offset in the <see cref="IFile"/> at which to begin reading.</param>
        /// <param name="destination">The buffer where the read bytes will be stored.
        /// The number of bytes read will be no larger than the length of the buffer.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        public Result Read(out long bytesRead, long offset, Span<byte> destination)
        {
            return Read(out bytesRead, offset, destination, ReadOption.None);
        }

        /// <summary>
        /// Writes a sequence of bytes to the current <see cref="IFile"/>.
        /// </summary>
        /// <param name="offset">The offset in the <see cref="IStorage"/> at which to begin writing.</param>
        /// <param name="source">The buffer containing the bytes to be written.</param>
        /// <param name="option">Options for writing to the <see cref="IFile"/>.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
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

        /// <summary>
        /// Causes any buffered data to be written to the underlying device.
        /// </summary>
        public Result Flush()
        {
            return DoFlush();
        }

        /// <summary>
        /// Sets the size of the file in bytes.
        /// </summary>
        /// <param name="size">The desired size of the file in bytes.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        public Result SetSize(long size)
        {
            if (size < 0)
                return ResultFs.OutOfRange.Log();

            return DoSetSize(size);
        }

        /// <summary>
        /// Gets the number of bytes in the file.
        /// </summary>
        /// <param name="size">If the operation returns successfully, the length of the file in bytes.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        public Result GetSize(out long size)
        {
            return DoGetSize(out size);
        }

        /// <summary>
        /// Performs various operations on the file. Used to extend the functionality of the <see cref="IFile"/> interface.
        /// </summary>
        /// <param name="outBuffer">A buffer that will contain the response from the operation.</param>
        /// <param name="operationId">The operation to be performed.</param>
        /// <param name="offset">The offset of the range to operate on.</param>
        /// <param name="size">The size of the range to operate on.</param>
        /// <param name="inBuffer">An input buffer. Size may vary depending on the operation performed.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        public Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            return DoOperateRange(outBuffer, operationId, offset, size, inBuffer);
        }

        /// <summary>
        /// Performs various operations on the file. Used to extend the functionality of the <see cref="IFile"/> interface.
        /// </summary>
        /// <param name="operationId">The operation to be performed.</param>
        /// <param name="offset">The offset of the range to operate on.</param>
        /// <param name="size">The size of the range to operate on.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        public Result OperateRange(OperationId operationId, long offset, long size)
        {
            return DoOperateRange(Span<byte>.Empty, operationId, offset, size, ReadOnlySpan<byte>.Empty);
        }

        protected Result DryRead(out long readableBytes, long offset, long size, in ReadOption option,
            OpenMode openMode)
        {
            UnsafeHelpers.SkipParamInit(out readableBytes);

            // Check that we can read.
            if (!openMode.HasFlag(OpenMode.Read))
                return ResultFs.ReadUnpermitted.Log();

            // Get the file size, and validate our offset.
            Result rc = GetSize(out long fileSize);
            if (rc.IsFailure()) return rc;

            if (offset > fileSize)
                return ResultFs.OutOfRange.Log();

            readableBytes = Math.Min(fileSize - offset, size);
            return Result.Success;
        }

        protected Result DrySetSize(long size, OpenMode openMode)
        {
            // Check that we can write.
            if (!openMode.HasFlag(OpenMode.Write))
                return ResultFs.WriteUnpermitted.Log();

            return Result.Success;
        }

        protected Result DryWrite(out bool needsAppend, long offset, long size, in WriteOption option,
            OpenMode openMode)
        {
            UnsafeHelpers.SkipParamInit(out needsAppend);

            // Check that we can write.
            if (!openMode.HasFlag(OpenMode.Write))
                return ResultFs.WriteUnpermitted.Log();

            // Get the file size.
            Result rc = GetSize(out long fileSize);
            if (rc.IsFailure()) return rc;

            if (fileSize < offset + size)
            {
                if (!openMode.HasFlag(OpenMode.AllowAppend))
                    return ResultFs.FileExtensionWithoutOpenModeAllowAppend.Log();

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
