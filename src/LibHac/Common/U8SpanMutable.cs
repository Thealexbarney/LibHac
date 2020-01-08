using System;
using System.Diagnostics;
using System.Text;

namespace LibHac.Common
{
    [DebuggerDisplay("{ToString()}")]
    public ref struct U8SpanMutable
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

        public bool IsNull() => _buffer == default;
    }
}
