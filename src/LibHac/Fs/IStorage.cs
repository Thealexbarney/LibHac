using System;
using System.Runtime.CompilerServices;
using System.Threading;
using LibHac.Common;

namespace LibHac.Fs
{
    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// Provides an interface for reading and writing a sequence of bytes.
    /// </summary>
    /// <remarks>
    /// The official IStorage makes the <c>Read</c> etc. methods abstract and doesn't
    /// have <c>DoRead</c> etc. methods. We're using them here so we can make sure
    /// the object isn't disposed before calling the method implementation.
    /// </remarks>
    public abstract class IStorage : IDisposable
    {
        // 0 = not disposed; 1 = disposed
        private int _disposedState;
        private bool IsDisposed => _disposedState != 0;

        /// <summary>
        /// Reads a sequence of bytes from the current <see cref="IStorage"/>.
        /// </summary>
        /// <param name="offset">The offset in the <see cref="IStorage"/> at which to begin reading.</param>
        /// <param name="destination">The buffer where the read bytes will be stored.
        /// The number of bytes read will be equal to the length of the buffer.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result Read(long offset, Span<byte> destination)
        {
            if (IsDisposed)
                return ResultFs.PreconditionViolation.Log();

            return DoRead(offset, destination);
        }

        /// <summary>
        /// Writes a sequence of bytes to the current <see cref="IStorage"/>.
        /// </summary>
        /// <param name="offset">The offset in the <see cref="IStorage"/> at which to begin writing.</param>
        /// <param name="source">The buffer containing the bytes to be written.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result Write(long offset, ReadOnlySpan<byte> source)
        {
            if (IsDisposed)
                return ResultFs.PreconditionViolation.Log();

            return DoWrite(offset, source);
        }

        /// <summary>
        /// Causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result Flush()
        {
            if (IsDisposed)
                return ResultFs.PreconditionViolation.Log();

            return DoFlush();
        }

        /// <summary>
        /// Sets the size of the current <see cref="IStorage"/>.
        /// </summary>
        /// <param name="size">The desired size of the <see cref="IStorage"/> in bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result SetSize(long size)
        {
            if (IsDisposed)
                return ResultFs.PreconditionViolation.Log();

            return DoSetSize(size);
        }

        /// <summary>
        /// Gets the number of bytes in the <see cref="IStorage"/>.
        /// </summary>
        /// <param name="size">If the operation returns successfully, the length of the file in bytes.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result GetSize(out long size)
        {
            UnsafeHelpers.SkipParamInit(out size);

            if (IsDisposed)
                return ResultFs.PreconditionViolation.Log();

            return DoGetSize(out size);
        }

        /// <summary>
        /// Performs various operations on the file. Used to extend the functionality of the <see cref="IStorage"/> interface.
        /// </summary>
        /// <param name="outBuffer">A buffer that will contain the response from the operation.</param>
        /// <param name="operationId">The operation to be performed.</param>
        /// <param name="offset">The offset of the range to operate on.</param>
        /// <param name="size">The size of the range to operate on.</param>
        /// <param name="inBuffer">An input buffer. Size may vary depending on the operation performed.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            if (IsDisposed)
                return ResultFs.PreconditionViolation.Log();

            return DoOperateRange(outBuffer, operationId, offset, size, inBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRangeValid(long offset, long size, long totalSize)
        {
            return offset >= 0 &&
                   size >= 0 &&
                   size <= totalSize &&
                   offset <= totalSize - size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOffsetAndSizeValid(long offset, long size)
        {
            return offset >= 0 &&
                   size >= 0 &&
                   offset <= offset + size;
        }

        protected abstract Result DoRead(long offset, Span<byte> destination);
        protected abstract Result DoWrite(long offset, ReadOnlySpan<byte> source);
        protected abstract Result DoFlush();
        protected abstract Result DoSetSize(long size);
        protected abstract Result DoGetSize(out long size);

        protected virtual Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            return ResultFs.NotImplemented.Log();
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
    }
}
