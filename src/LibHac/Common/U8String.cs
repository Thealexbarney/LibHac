using System;
using System.Diagnostics;
using System.Text;
using LibHac.Util;

namespace LibHac.Common
{
    [DebuggerDisplay("{ToString()}")]
    public readonly struct U8String
    {
        private readonly byte[] _buffer;

        public ReadOnlySpan<byte> Value => _buffer;
        public int Length => _buffer.Length;

        public byte this[int i] => _buffer[i];

        public U8String(byte[] value)
        {
            _buffer = value;
        }

        public U8String(string value)
        {
            _buffer = Encoding.UTF8.GetBytes(value);
        }

        public U8String Slice(int start)
        {
            return new U8String(_buffer.AsSpan(start).ToArray());
        }

        public U8String Slice(int start, int length)
        {
            return new U8String(_buffer.AsSpan(start, length).ToArray());
        }

        public static implicit operator U8Span(U8String value) => new U8Span(value._buffer);

        public static implicit operator ReadOnlySpan<byte>(U8String value) => value.Value;

        public static explicit operator string(U8String value) => value.ToString();
        public static explicit operator U8String(string value) => new U8String(value);

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
