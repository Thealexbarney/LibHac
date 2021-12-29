using System;
using System.Runtime.CompilerServices;

namespace LibHac.Fs;

// ReSharper disable once InconsistentNaming
/// <summary>
/// Provides an interface for reading and writing a sequence of bytes.
/// </summary>
/// <remarks>
/// The official IStorage makes the <c>Read</c> etc. methods abstract and doesn't
/// have <c>DoRead</c> etc. methods. We're using them here so we can make sure
/// the object isn't disposed before calling the method implementation.
/// </remarks>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public abstract class IStorage : IDisposable
{
    public virtual void Dispose() { }

    /// <summary>
    /// Reads a sequence of bytes from the current <see cref="IStorage"/>.
    /// </summary>
    /// <param name="offset">The offset in the <see cref="IStorage"/> at which to begin reading.</param>
    /// <param name="destination">The buffer where the read bytes will be stored.
    /// The number of bytes read will be equal to the length of the buffer.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public abstract Result Read(long offset, Span<byte> destination);

    /// <summary>
    /// Writes a sequence of bytes to the current <see cref="IStorage"/>.
    /// </summary>
    /// <param name="offset">The offset in the <see cref="IStorage"/> at which to begin writing.</param>
    /// <param name="source">The buffer containing the bytes to be written.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public abstract Result Write(long offset, ReadOnlySpan<byte> source);

    /// <summary>
    /// Causes any buffered data to be written to the underlying device.
    /// </summary>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public abstract Result Flush();

    /// <summary>
    /// Sets the size of the current <see cref="IStorage"/>.
    /// </summary>
    /// <param name="size">The desired size of the <see cref="IStorage"/> in bytes.</param>
    public abstract Result SetSize(long size);

    /// <summary>
    /// Gets the number of bytes in the <see cref="IStorage"/>.
    /// </summary>
    /// <param name="size">If the operation returns successfully, the length of the file in bytes.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public abstract Result GetSize(out long size);

    /// <summary>
    /// Performs various operations on the storage. Used to extend the functionality of the <see cref="IStorage"/> interface.
    /// </summary>
    /// <param name="outBuffer">A buffer that will contain the response from the operation.</param>
    /// <param name="operationId">The operation to be performed.</param>
    /// <param name="offset">The offset of the range to operate on.</param>
    /// <param name="size">The size of the range to operate on.</param>
    /// <param name="inBuffer">An input buffer. Size may vary depending on the operation performed.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public abstract Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer);

    /// <summary>
    /// Performs various operations on the storage. Used to extend the functionality of the <see cref="IStorage"/> interface.
    /// </summary>
    /// <param name="operationId">The operation to be performed.</param>
    /// <param name="offset">The offset of the range to operate on.</param>
    /// <param name="size">The size of the range to operate on.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result OperateRange(OperationId operationId, long offset, long size)
    {
        return OperateRange(Span<byte>.Empty, operationId, offset, size, ReadOnlySpan<byte>.Empty);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CheckAccessRange(long offset, long size, long totalSize)
    {
        return offset >= 0 &&
               size >= 0 &&
               size <= totalSize &&
               offset <= totalSize - size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CheckOffsetAndSize(long offset, long size)
    {
        return offset >= 0 &&
               size >= 0 &&
               offset <= offset + size;
    }
}