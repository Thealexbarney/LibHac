using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LibHac.Util;

namespace LibHac.Common
{
    [DebuggerDisplay("{ToString()}")]
    public readonly ref struct U8Span
    {
        private readonly ReadOnlySpan<byte> _buffer;

        public ReadOnlySpan<byte> Value => _buffer;
        public int Length => _buffer.Length;

        public static U8Span Empty => default;

        public byte this[int i]
        {
            get => _buffer[i];
        }

        public U8Span(ReadOnlySpan<byte> value)
        {
            _buffer = value;
        }

        public U8Span(string value)
        {
            _buffer = Encoding.UTF8.GetBytes(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetOrNull(int i)
        {
            byte value = 0;

            if ((uint)i < (uint)_buffer.Length)
            {
                value = GetUnsafe(i);
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetUnsafe(int i)
        {
#if DEBUG
            return _buffer[i];
#else
            return Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), i);
#endif
        }

        public U8Span Slice(int start)
        {
            return new U8Span(_buffer.Slice(start));
        }

        public U8Span Slice(int start, int length)
        {
            return new U8Span(_buffer.Slice(start, length));
        }

        public static implicit operator ReadOnlySpan<byte>(in U8Span value) => value.Value;

        public static explicit operator string(in U8Span value) => value.ToString();
        public static explicit operator U8Span(string value) => new U8Span(value);

        public override string ToString()
        {
            return StringUtils.Utf8ZToString(_buffer);
        }

        public U8String ToU8String()
        {
            int length = StringUtils.GetLength(_buffer);

            // Allocate an extra byte for the null terminator
            byte[] buffer = new byte[length + 1];
            _buffer.Slice(0, length).CopyTo(buffer);

            return new U8String(buffer);
        }

        /// <summary>
        /// Checks if the <see cref="U8Span"/> has no buffer.
        /// </summary>
        /// <returns><see langword="true"/> if the span has no buffer.
        /// Otherwise, <see langword="false"/>.</returns>
        public bool IsNull() => Unsafe.IsNullRef(ref MemoryMarshal.GetReference(_buffer));

        /// <summary>
        /// Checks if the <see cref="U8Span"/> has no buffer or begins with a null terminator.
        /// </summary>
        /// <returns><see langword="true"/> if the span has no buffer or begins with a null terminator.
        /// Otherwise, <see langword="false"/>.</returns>
        public bool IsEmpty() => _buffer.IsEmpty || MemoryMarshal.GetReference(_buffer) == 0;
    }
}
