using System;
using System.Runtime.CompilerServices;
using LibHac.Common;

namespace LibHac.Sf
{
    public readonly ref struct InBuffer
    {
        private readonly ReadOnlySpan<byte> _buffer;

        public int Size => _buffer.Length;
        public ReadOnlySpan<byte> Buffer => _buffer;

        public InBuffer(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static InBuffer FromStruct<T>(in T value) where T : unmanaged
        {
            return new InBuffer(SpanHelpers.AsReadOnlyByteSpan(in value));
        }
    }

    public readonly ref struct OutBuffer
    {
        private readonly Span<byte> _buffer;

        public int Size => _buffer.Length;
        public Span<byte> Buffer => _buffer;

        public OutBuffer(Span<byte> buffer)
        {
            _buffer = buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OutBuffer FromStruct<T>(ref T value) where T : unmanaged
        {
            return new OutBuffer(SpanHelpers.AsByteSpan(ref value));
        }
    }

    public readonly ref struct InArray<T> where T : unmanaged
    {
        private readonly ReadOnlySpan<T> _array;

        public int Size => _array.Length;
        public ReadOnlySpan<T> Array => _array;

        public InArray(ReadOnlySpan<T> array)
        {
            _array = array;
        }

        public ref readonly T this[int i] => ref _array[i];
    }

    public readonly ref struct OutArray<T> where T : unmanaged
    {
        private readonly Span<T> _array;

        public int Size => _array.Length;
        public Span<T> Array => _array;

        public OutArray(Span<T> array)
        {
            _array = array;
        }

        public ref T this[int i] => ref _array[i];
    }
}
