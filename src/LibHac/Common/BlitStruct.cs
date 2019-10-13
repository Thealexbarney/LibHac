using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common
{
    public ref struct BlitStruct<T> where T : unmanaged
    {
        private readonly Span<T> _buffer;

        public int Length => _buffer.Length;

        public ref T Value => ref _buffer[0];
        public ref T this[int index] => ref _buffer[index];

        public BlitStruct(Span<T> data)
        {
            _buffer = data;

            Debug.Assert(_buffer.Length != 0);
        }

        public BlitStruct(Span<byte> data)
        {
            _buffer = MemoryMarshal.Cast<byte, T>(data);

            Debug.Assert(_buffer.Length != 0);
        }

        public BlitStruct(ref T data)
        {
            _buffer = SpanHelpers.AsSpan(ref data);
        }

        public Span<byte> GetByteSpan()
        {
            return MemoryMarshal.Cast<T, byte>(_buffer);
        }

        public Span<byte> GetByteSpan(int elementIndex)
        {
            Span<T> element = _buffer.Slice(elementIndex, 1);
            return MemoryMarshal.Cast<T, byte>(element);
        }

        public static int QueryByteLength(int elementCount)
        {
            return Unsafe.SizeOf<T>() * elementCount;
        }
    }
}
