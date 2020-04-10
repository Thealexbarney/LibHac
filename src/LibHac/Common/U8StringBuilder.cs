using System;
using System.Buffers;
using System.Buffers.Text;
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
        public readonly int Length => _length;
        public readonly int Capacity => _buffer.Length - NullTerminatorLength;
        public readonly Span<byte> Buffer => _buffer;

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

        public U8StringBuilder AppendFormat(byte value, char format = 'G', byte precision = 255) =>
            AppendFormatUInt64(value, format, precision);

        public U8StringBuilder AppendFormat(sbyte value, char format = 'G', byte precision = 255) =>
            AppendFormatInt64(value, 0xff, format, precision);

        public U8StringBuilder AppendFormat(ushort value, char format = 'G', byte precision = 255) =>
            AppendFormatUInt64(value, format, precision);

        public U8StringBuilder AppendFormat(short value, char format = 'G', byte precision = 255) =>
            AppendFormatInt64(value, 0xffff, format, precision);

        public U8StringBuilder AppendFormat(uint value, char format = 'G', byte precision = 255) =>
            AppendFormatUInt64(value, format, precision);

        public U8StringBuilder AppendFormat(int value, char format = 'G', byte precision = 255) =>
            AppendFormatInt64(value, 0xffffff, format, precision);

        public U8StringBuilder AppendFormat(ulong value, char format = 'G', byte precision = 255) =>
            AppendFormatUInt64(value, format, precision);

        public U8StringBuilder AppendFormat(long value, char format = 'G', byte precision = 255) =>
            AppendFormatInt64(value, 0xffffffff, format, precision);

        public U8StringBuilder AppendFormat(float value, char format = 'G', byte precision = 255) =>
            AppendFormatFloat(value, format, precision);

        public U8StringBuilder AppendFormat(double value, char format = 'G', byte precision = 255) =>
            AppendFormatDouble(value, format, precision);

        private readonly bool HasCapacity(int requiredCapacity)
        {
            return requiredCapacity <= Capacity;
        }

        private readonly bool HasAdditionalCapacity(int requiredAdditionalCapacity)
        {
            return HasCapacity(_length + requiredAdditionalCapacity);
        }

        private void AddNullTerminator()
        {
            _buffer[_length] = 0;
        }

        private readonly void ThrowIfBufferLengthIsZero()
        {
            if (_buffer.Length == 0) throw new ArgumentException("Buffer length must be greater than 0.");
        }

        private U8StringBuilder AppendFormatInt64(long value, ulong mask, char format, byte precision)
        {
            if (Overflowed) return this;

            // Remove possible sign extension if needed
            if (mask == 'x' | mask == 'X')
            {
                value &= (long)mask;
            }

            // Exclude the null terminator from the buffer because Utf8Formatter doesn't handle it
            Span<byte> availableBuffer = _buffer.Slice(_length, Capacity - _length);

            bool bufferLargeEnough = Utf8Formatter.TryFormat(value, availableBuffer, out int bytesWritten,
                new StandardFormat(format, precision));

            if (!bufferLargeEnough)
            {
                Overflowed = true;
                return this;
            }

            _length += bytesWritten;
            AddNullTerminator();

            return this;
        }

        private U8StringBuilder AppendFormatUInt64(ulong value, char format, byte precision)
        {
            if (Overflowed) return this;

            // Exclude the null terminator from the buffer because Utf8Formatter doesn't handle it
            Span<byte> availableBuffer = _buffer.Slice(_length, Capacity - _length);

            bool bufferLargeEnough = Utf8Formatter.TryFormat(value, availableBuffer, out int bytesWritten,
                new StandardFormat(format, precision));

            if (!bufferLargeEnough)
            {
                Overflowed = true;
                return this;
            }

            _length += bytesWritten;
            AddNullTerminator();

            return this;
        }

        private U8StringBuilder AppendFormatFloat(float value, char format, byte precision)
        {
            if (Overflowed) return this;

            // Exclude the null terminator from the buffer because Utf8Formatter doesn't handle it
            Span<byte> availableBuffer = _buffer.Slice(_length, Capacity - _length);

            bool bufferLargeEnough = Utf8Formatter.TryFormat(value, availableBuffer, out int bytesWritten,
                new StandardFormat(format, precision));

            if (!bufferLargeEnough)
            {
                Overflowed = true;
                return this;
            }

            _length += bytesWritten;
            AddNullTerminator();

            return this;
        }

        private U8StringBuilder AppendFormatDouble(double value, char format, byte precision)
        {
            if (Overflowed) return this;

            // Exclude the null terminator from the buffer because Utf8Formatter doesn't handle it
            Span<byte> availableBuffer = _buffer.Slice(_length, Capacity - _length);

            bool bufferLargeEnough = Utf8Formatter.TryFormat(value, availableBuffer, out int bytesWritten,
                new StandardFormat(format, precision));

            if (!bufferLargeEnough)
            {
                Overflowed = true;
                return this;
            }

            _length += bytesWritten;
            AddNullTerminator();

            return this;
        }

        public override readonly string ToString() => StringUtils.Utf8ZToString(_buffer);
    }
}
