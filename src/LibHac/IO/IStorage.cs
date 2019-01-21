using System;

namespace LibHac.IO
{
    public interface IStorage : IDisposable
    {
        /// <summary>
        /// Reads a sequence of bytes from the current <see cref="IStorage"/>.
        /// </summary>
        /// <param name="destination">The buffer where the read bytes will be stored.
        /// The number of bytes read will be equal to the length of the buffer.</param>
        /// <param name="offset">The offset in the <see cref="IStorage"/> to begin reading from.</param>
        void Read(Span<byte> destination, long offset);

        /// <summary>
        /// Writes a sequence of bytes to the current <see cref="IStorage"/>.
        /// </summary>
        /// <param name="source">The buffer containing the bytes to be written.</param>
        /// <param name="offset">The offset in the <see cref="IStorage"/> to begin writing to.</param>
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
