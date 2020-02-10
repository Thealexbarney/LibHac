using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace LibHac.Common
{
    [DebuggerDisplay("{ToString()}")]
    public readonly ref struct U8Span
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

        public static implicit operator ReadOnlySpan<byte>(U8Span value) => value.Value;

        public static explicit operator string(U8Span value) => value.ToString();
        public static explicit operator U8Span(string value) => new U8Span(value);

        public override string ToString()
        {
            return StringUtils.Utf8ZToString(_buffer);
        }

        public U8String ToU8String()
        {
            return new U8String(_buffer.ToArray());
        }

        public bool IsEmpty() => _buffer.IsEmpty;
    }
}
