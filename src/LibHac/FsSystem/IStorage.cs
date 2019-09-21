using System;

namespace LibHac.FsSystem
{
    /// <summary>
    /// Provides an interface for reading and writing a sequence of bytes.
    /// </summary>
    public interface IStorage : IDisposable
    {
        /// <summary>
        /// Reads a sequence of bytes from the current <see cref="IStorage"/>.
        /// </summary>
        /// <param name="destination">The buffer where the read bytes will be stored.
        /// The number of bytes read will be equal to the length of the buffer.</param>
        /// <param name="offset">The offset in the <see cref="IStorage"/> at which to begin reading.</param>
        /// <exception cref="ArgumentException">Invalid offset or the IStorage contains fewer bytes than requested. </exception>
        Result Read(long offset, Span<byte> destination);

        /// <summary>
        /// Writes a sequence of bytes to the current <see cref="IStorage"/>.
        /// </summary>
        /// <param name="source">The buffer containing the bytes to be written.</param>
        /// <param name="offset">The offset in the <see cref="IStorage"/> at which to begin writing.</param>
        /// <exception cref="ArgumentException">Invalid offset or <paramref name="source"/>
        /// is too large to be written to the IStorage. </exception>
        Result Write(long offset, ReadOnlySpan<byte> source);

        /// <summary>
        /// Causes any buffered data to be written to the underlying device.
        /// </summary>
        Result Flush();

        /// <summary>
        /// Sets the size of the current IStorage.
        /// </summary>
        /// <param name="size">The desired size of the current IStorage in bytes.</param>
        Result SetSize(long size);

        /// <summary>
        /// The size of the<see cref="IStorage"/>. -1 will be returned if
        /// the <see cref="IStorage"/> cannot be represented as a sequence of contiguous bytes.
        /// </summary>
        /// <returns>The size of the <see cref="IStorage"/> in bytes.</returns>
        Result GetSize(out long size);
    }
}
