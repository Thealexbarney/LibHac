using System;
using System.Diagnostics;
using System.Text;
using LibHac.Util;

namespace LibHac.Common
{
    [DebuggerDisplay("{ToString()}")]
    public readonly struct U8StringMutable
    {
        private readonly byte[] _buffer;

        public Span<byte> Value => _buffer;
        public int Length => _buffer.Length;

        public byte this[int i]
        {
            get => _buffer[i];
            set => _buffer[i] = value;
        }

        public U8StringMutable(byte[] value)
        {
            _buffer = value;
        }

        public U8StringMutable(string value)
        {
            _buffer = Encoding.UTF8.GetBytes(value);
        }

        public U8StringMutable Slice(int start)
        {
            return new U8StringMutable(_buffer.AsSpan(start).ToArray());
        }

        public U8StringMutable Slice(int start, int length)
        {
            return new U8StringMutable(_buffer.AsSpan(start, length).ToArray());
        }

        public static implicit operator U8String(U8StringMutable value) => new U8String(value._buffer);
        public static implicit operator U8SpanMutable(U8StringMutable value) => new U8SpanMutable(value._buffer);
        public static implicit operator U8Span(U8StringMutable value) => new U8Span(value._buffer);

        public static implicit operator ReadOnlySpan<byte>(U8StringMutable value) => value.Value;
        public static implicit operator Span<byte>(U8StringMutable value) => value.Value;

        public static explicit operator string(U8StringMutable value) => value.ToString();
        public static explicit operator U8StringMutable(string value) => new U8StringMutable(value);

        public override string ToString()
        {
            return StringUtils.Utf8ZToString(_buffer);
        }

        /// <summary>
        /// Checks if the <see cref="U8String"/> has no buffer.
        /// </summary>
        /// <returns><see langword="true"/> if the string has no buffer.
        /// Otherwise, <see langword="false"/>.</returns>
        public bool IsNull() => _buffer == null;

        /// <summary>
        /// Checks if the <see cref="U8String"/> has no buffer or begins with a null terminator.
        /// </summary>
        /// <returns><see langword="true"/> if the string has no buffer or begins with a null terminator.
        /// Otherwise, <see langword="false"/>.</returns>
        public bool IsEmpty() => _buffer == null || _buffer.Length < 1 || _buffer[0] == 0;
    }
}
