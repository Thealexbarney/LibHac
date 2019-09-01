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
        /// The permissions mode for the current file.
        /// </summary>
        OpenMode Mode { get; }

        /// <summary>
        /// Reads a sequence of bytes from the current <see cref="IFile"/>.
        /// </summary>
        /// <param name="destination">The buffer where the read bytes will be stored.
        /// The number of bytes read will be no larger than the length of the buffer.</param>
        /// <param name="offset">The offset in the <see cref="IFile"/> at which to begin reading.</param>
        /// <param name="options">Options for reading from the <see cref="IFile"/>.</param>
        /// <returns>The total number of bytes read into the buffer. This can be less than the
        /// size of the buffer if the IFile is too short to fulfill the request.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is invalid.</exception>
        /// <exception cref="NotSupportedException">The file's <see cref="OpenMode"/> does not allow reading.</exception>
        int Read(Span<byte> destination, long offset, ReadOption options);

        /// <summary>
        /// Writes a sequence of bytes to the current <see cref="IFile"/>.
        /// </summary>
        /// <param name="source">The buffer containing the bytes to be written.</param>
        /// <param name="offset">The offset in the <see cref="IStorage"/> at which to begin writing.</param>
        /// <param name="options">Options for writing to the <see cref="IFile"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative.</exception>
        /// <exception cref="NotSupportedException">The file's <see cref="OpenMode"/> does not allow this request.</exception>
        void Write(ReadOnlySpan<byte> source, long offset, WriteOption options);

        /// <summary>
        /// Causes any buffered data to be written to the underlying device.
        /// </summary>
        void Flush();

        /// <summary>
        /// Gets the number of bytes in the file.
        /// </summary>
        /// <returns>The length of the file in bytes.</returns>
        long GetSize();

        /// <summary>
        /// Sets the size of the file in bytes.
        /// </summary>
        /// <param name="size">The desired size of the file in bytes.</param>
        /// <exception cref="NotSupportedException">If increasing the file size, The file's
        /// <see cref="OpenMode"/> does not allow this appending.</exception>
        void SetSize(long size);
    }
}