using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LibHac.Util;

namespace LibHac.Common
{
    [DebuggerDisplay("{ToString()}")]
    public readonly ref struct U8SpanMutable
    {
        private readonly Span<byte> _buffer;

        public Span<byte> Value => _buffer;
        public int Length => _buffer.Length;

        public byte this[int i]
        {
            get => _buffer[i];
            set => _buffer[i] = value;
        }

        public U8SpanMutable(Span<byte> value)
        {
            _buffer = value;
        }

        public U8SpanMutable(string value)
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

        public U8SpanMutable Slice(int start)
        {
            return new U8SpanMutable(_buffer.Slice(start));
        }

        public U8SpanMutable Slice(int start, int length)
        {
            return new U8SpanMutable(_buffer.Slice(start, length));
        }

        public static implicit operator U8Span(U8SpanMutable value) => new U8Span(value._buffer);

        public static implicit operator ReadOnlySpan<byte>(U8SpanMutable value) => value.Value;
        public static implicit operator Span<byte>(U8SpanMutable value) => value.Value;

        public static explicit operator string(U8SpanMutable value) => value.ToString();
        public static explicit operator U8SpanMutable(string value) => new U8SpanMutable(value);

        public override string ToString()
        {
            return StringUtils.Utf8ZToString(_buffer);
        }

        public U8StringMutable ToU8String()
        {
            return new U8StringMutable(_buffer.ToArray());
        }

        /// <summary>
        /// Checks if the <see cref="U8StringMutable"/> has no buffer.
        /// </summary>
        /// <returns><see langword="true"/> if the span has no buffer.
        /// Otherwise, <see langword="false"/>.</returns>
        public bool IsNull() => Unsafe.IsNullRef(ref MemoryMarshal.GetReference(_buffer));

        /// <summary>
        /// Checks if the <see cref="U8StringMutable"/> has no buffer or begins with a null terminator.
        /// </summary>
        /// <returns><see langword="true"/> if the span has no buffer or begins with a null terminator.
        /// Otherwise, <see langword="false"/>.</returns>
        public bool IsEmpty() => _buffer.IsEmpty || MemoryMarshal.GetReference(_buffer) == 0;
    }
}
