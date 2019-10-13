using System;
using System.Diagnostics;
using System.Text;

namespace LibHac.Common
{
    [DebuggerDisplay("{ToString()}")]
    public ref struct U8Span
    {
        private readonly ReadOnlySpan<byte> _buffer;

        public ReadOnlySpan<byte> Value => _buffer;
        public int Length => _buffer.Length;

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

        public static implicit operator ReadOnlySpan<byte>(U8Span value) => value.Value;

        public static explicit operator string(U8Span value) => value.ToString();
        public static explicit operator U8Span(string value) => new U8Span(value);

        public override string ToString()
        {
            return StringUtils.Utf8ToString(_buffer);
        }

        public bool IsNull() => _buffer == default;
    }
}
