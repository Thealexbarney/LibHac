using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common
{
    /// <summary>
    /// Provides a representation of a region of memory as if it were a series of blittable structs
    /// of type <typeparamref name="T"/>. Also allows viewing the memory as a <see cref="Span{T}"/> of bytes.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public ref struct BlitSpan<T> where T : unmanaged
    {
        private readonly Span<T> _buffer;

        /// <summary>
        /// The number of elements of type <typeparamref name="T"/> in the <see cref="BlitSpan{T}"/>.
        /// </summary>
        public int Length => _buffer.Length;

        /// <summary>
        /// A reference to the first element in this collection.
        /// </summary>
        public ref T Value
        {
            get
            {
                Debug.Assert(_buffer.Length > 0);
                
                return ref MemoryMarshal.GetReference(_buffer);
            }
        }

        /// <summary>
        /// A reference to the element at index <paramref name="index"/>.
        /// </summary>
        public ref T this[int index] => ref _buffer[index];

        /// <summary>
        /// Creates a new <see cref="BlitSpan{T}"/> using the provided <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="data">The span from which to create the <see cref="BlitSpan{T}"/>.
        /// Must have a length of at least 1.</param>
        public BlitSpan(Span<T> data)
        {
            if (data.Length == 0)
                ThrowHelper.ThrowArgumentOutOfRangeException();

            _buffer = data;
        }

        /// <summary>
        /// Creates a new <see cref="BlitSpan{T}"/> using the provided <see cref="Span{T}"/> of bytes.
        /// </summary>
        /// <param name="data">The byte span from which to create the <see cref="BlitSpan{T}"/>.
        /// Must be long enough to hold at least one struct of type <typeparamref name="T"/></param>
        public BlitSpan(Span<byte> data)
        {
            if (data.Length < Unsafe.SizeOf<T>())
                ThrowHelper.ThrowArgumentOutOfRangeException();

            _buffer = MemoryMarshal.Cast<byte, T>(data);
        }

        /// <summary>
        /// Creates a new <see cref="BlitSpan{T}"/> over a struct of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="data">The struct from which to create the <see cref="BlitSpan{T}"/>.</param>
        public BlitSpan(ref T data)
        {
            _buffer = SpanHelpers.AsSpan(ref data);
        }

        /// <summary>
        /// A <see cref="Span{T}"/> of the elements in the <see cref="BlitSpan{T}"/>.
        /// </summary>
        public Span<T> Span => _buffer;

        /// <summary>
        /// Returns a view of the <see cref="BlitSpan{T}"/> as a <see cref="Span{T}"/> of bytes.
        /// </summary>
        /// <returns>A byte span representation of the <see cref="BlitSpan{T}"/>.</returns>
        public Span<byte> GetByteSpan()
        {
            return MemoryMarshal.Cast<T, byte>(_buffer);
        }

        /// <summary>
        /// Returns a view of the element at index <paramref name="elementIndex"/> as a <see cref="Span{T}"/> of bytes.
        /// </summary>
        /// <param name="elementIndex">The zero-based index of the element.</param>
        /// <returns>A byte span representation of the element.</returns>
        public Span<byte> GetByteSpan(int elementIndex)
        {
            return SpanHelpers.AsByteSpan(ref _buffer[elementIndex]);
        }

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
