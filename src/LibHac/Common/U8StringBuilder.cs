using System;
using System.Diagnostics;

namespace LibHac.Common
{
    [DebuggerDisplay("{ToString()}")]
    public ref struct U8StringBuilder
    {
        private const int NullTerminatorLength = 1;

        private readonly Span<byte> _buffer;
        private int _length;

        public bool Overflowed { get; private set; }
        public int Length => _length;
        public int Capacity => _buffer.Length - NullTerminatorLength;

        public U8StringBuilder(Span<byte> buffer)
        {
            _buffer = buffer;
            _length = 0;
            Overflowed = false;

            ThrowIfBufferLengthIsZero();

            AddNullTerminator();
        }

        public U8StringBuilder Append(ReadOnlySpan<byte> value)
        {
            if (Overflowed) return this;

            int valueLength = StringUtils.GetLength(value);

            if (!HasAdditionalCapacity(valueLength))
            {
                Overflowed = true;
                return this;
            }

            value.Slice(0, valueLength).CopyTo(_buffer.Slice(_length));
            _length += valueLength;
            AddNullTerminator();

            return this;
        }

        public U8StringBuilder Append(byte value)
        {
            if (Overflowed) return this;

            if (!HasAdditionalCapacity(1))
            {
                Overflowed = true;
                return this;
            }

            _buffer[_length] = value;
            _length++;
            AddNullTerminator();

            return this;
        }

        private bool HasCapacity(int requiredCapacity)
        {
            return requiredCapacity <= Capacity;
        }

        private bool HasAdditionalCapacity(int requiredAdditionalCapacity)
        {
            return HasCapacity(_length + requiredAdditionalCapacity);
        }

        private void AddNullTerminator()
        {
            _buffer[_length] = 0;
        }

        private void ThrowIfBufferLengthIsZero()
        {
            if (_buffer.Length == 0) throw new ArgumentException("Buffer length must be greater than 0.");
        }

        public override string ToString() => StringUtils.Utf8ZToString(_buffer);
    }
}
