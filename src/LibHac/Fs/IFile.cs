using System;

namespace LibHac.Fs
{
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
    public interface IFile : IDisposable
    {
        /// <summary>
        /// Reads a sequence of bytes from the current <see cref="IFile"/>.
        /// </summary>
        /// <param name="bytesRead">If the operation returns successfully, The total number of bytes read into
        /// the buffer. This can be less than the size of the buffer if the IFile is too short to fulfill the request.</param>
        /// <param name="offset">The offset in the <see cref="IFile"/> at which to begin reading.</param>
        /// <param name="destination">The buffer where the read bytes will be stored.
        /// The number of bytes read will be no larger than the length of the buffer.</param>
        /// <param name="options">Options for reading from the <see cref="IFile"/>.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOptionFlag options);

        /// <summary>
        /// Writes a sequence of bytes to the current <see cref="IFile"/>.
        /// </summary>
        /// <param name="offset">The offset in the <see cref="IStorage"/> at which to begin writing.</param>
        /// <param name="source">The buffer containing the bytes to be written.</param>
        /// <param name="options">Options for writing to the <see cref="IFile"/>.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        Result Write(long offset, ReadOnlySpan<byte> source, WriteOptionFlag options);

        /// <summary>
        /// Causes any buffered data to be written to the underlying device.
        /// </summary>
        Result Flush();

        /// <summary>
        /// Sets the size of the file in bytes.
        /// </summary>
        /// <param name="size">The desired size of the file in bytes.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        Result SetSize(long size);

        /// <summary>
        /// Gets the number of bytes in the file.
        /// </summary>
        /// <param name="size">If the operation returns successfully, the length of the file in bytes.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        Result GetSize(out long size);

        /// <summary>
        /// Performs various operations on the file. Used to extend the functionality of the <see cref="IFile"/> interface.
        /// </summary>
        /// <param name="outBuffer">A buffer that will contain the response from the operation.</param>
        /// <param name="operationId">The operation to be performed.</param>
        /// <param name="offset">The offset of the range to operate on.</param>
        /// <param name="size">The size of the range to operate on.</param>
        /// <param name="inBuffer">An input buffer. Size may vary depending on the operation performed.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer);
    }
}