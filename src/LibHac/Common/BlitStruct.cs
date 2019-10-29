using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common
{
    /// <summary>
    /// Handles storing a blittable struct or a series of blittable structs in a byte array.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public readonly struct BlitStruct<T> where T : unmanaged
    {
        private readonly byte[] _buffer;

        public int Length => _buffer.Length / Unsafe.SizeOf<T>();

        /// <summary>
        /// A reference to the first element in this collection.
        /// </summary>
        public ref T Value
        {
            get
            {
                Debug.Assert(_buffer.Length >= Unsafe.SizeOf<T>());

                return ref Unsafe.As<byte, T>(ref _buffer[0]);
            }
        }

        /// <summary>
        /// Initializes a new <see cref="BlitStruct{T}"/> that can hold the specified number
        /// of elements of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="elementCount">The number of elements the <see cref="BlitStruct{T}"/> will be able to store.</param>
        public BlitStruct(int elementCount)
        {
            if (elementCount <= 0)
                ThrowHelper.ThrowArgumentOutOfRangeException();

            _buffer = new byte[QueryByteLength(elementCount)];
        }

        /// <summary>
        /// Returns a <see cref="Span"/> view of the elements in the current <see cref="BlitStruct{T}"/> as type <typeparamref name="T"/>.
        /// </summary>
        public Span<T> Span => MemoryMarshal.Cast<byte, T>(_buffer);

        /// <summary>
        /// Returns a <see cref="Span"/> view of the elements in the current <see cref="BlitStruct{T}"/> as <see cref="byte"/>s.
        /// </summary>
        public Span<byte> ByteSpan => _buffer;

        /// <summary>
        /// Creates a <see cref="BlitSpan{T}"/> from the current <see cref="BlitStruct{T}"/>.
        /// </summary>
        public BlitSpan<T> BlitSpan => new BlitSpan<T>(_buffer);

        /// <summary>
        /// Calculates the length of memory in bytes that would be needed to store <paramref name="elementCount"/>
        /// elements of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="elementCount">The number of elements.</param>
        /// <returns>The number of bytes required.</returns>
        public static int QueryByteLength(int elementCount)
        {
            return Unsafe.SizeOf<T>() * elementCount;
        }
    }
}
