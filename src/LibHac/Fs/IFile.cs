using System;
using LibHac.FsSystem;

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
        /// The permissions mode for the current file.
        /// </summary>
        OpenMode Mode { get; }

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
        Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption options);

        /// <summary>
        /// Writes a sequence of bytes to the current <see cref="IFile"/>.
        /// </summary>
        /// <param name="offset">The offset in the <see cref="IStorage"/> at which to begin writing.</param>
        /// <param name="source">The buffer containing the bytes to be written.</param>
        /// <param name="options">Options for writing to the <see cref="IFile"/>.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        Result Write(long offset, ReadOnlySpan<byte> source, WriteOption options);

        /// <summary>
        /// Causes any buffered data to be written to the underlying device.
        /// </summary>
        Result Flush();

        /// <summary>
        /// Gets the number of bytes in the file.
        /// </summary>
        /// <param name="size">If the operation returns successfully, the length of the file in bytes.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        Result GetSize(out long size);

        /// <summary>
        /// Sets the size of the file in bytes.
        /// </summary>
        /// <param name="size">The desired size of the file in bytes.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        Result SetSize(long size);
    }
}