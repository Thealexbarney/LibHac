using System;
using System.Diagnostics;
using System.Text;

namespace LibHac.Common
{
    [DebuggerDisplay("{ToString()}")]
    public struct U8String
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

        public static implicit operator U8Span(U8String value) => new U8Span(value._buffer);

        public static implicit operator ReadOnlySpan<byte>(U8String value) => value.Value;

        public static explicit operator string(U8String value) => value.ToString();
        public static explicit operator U8String(string value) => new U8String(value);

        public override string ToString()
        {
            return StringUtils.Utf8ToString(_buffer);
        }

        public bool IsNull() => _buffer == null;
    }
}
