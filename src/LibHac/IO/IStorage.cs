using System;

namespace LibHac.IO
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
        void Read(Span<byte> destination, long offset);

        /// <summary>
        /// Writes a sequence of bytes to the current <see cref="IStorage"/>.
        /// </summary>
        /// <param name="source">The buffer containing the bytes to be written.</param>
        /// <param name="offset">The offset in the <see cref="IStorage"/> at which to begin writing.</param>
        /// <exception cref="ArgumentException">Invalid offset or <paramref name="source"/>
        /// is too large to be written to the IStorage. </exception>
        void Write(ReadOnlySpan<byte> source, long offset);

        /// <summary>
        /// Causes any buffered data to be written to the underlying device.
        /// </summary>
        void Flush();

        /// <summary>
        /// The length of the <see cref="IStorage"/>. -1 will be returned if
        /// the <see cref="IStorage"/> cannot be represented as a sequence of contiguous bytes.
        /// </summary>
        long Length { get; }
    }
}
