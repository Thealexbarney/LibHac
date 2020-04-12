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

        public Span<byte> Buffer { get; }
        public int Length { get; private set; }
        public bool Overflowed { get; private set; }
        public readonly int Capacity => Buffer.Length - NullTerminatorLength;

        public U8StringBuilder(Span<byte> buffer)
        {
            Buffer = buffer;
            Length = 0;
            Overflowed = false;

            ThrowIfBufferLengthIsZero();

            AddNullTerminator();
        }

        // These functions are internal so they can be called by the extension methods
        // in U8StringBuilderExtensions. It's not an ideal setup, but it allows append
        // calls to be chained without accidentally creating a copy of the U8StringBuilder.
        internal void AppendInternal(ReadOnlySpan<byte> value)
        {
            if (Overflowed) return;

            int valueLength = StringUtils.GetLength(value);

            if (!HasAdditionalCapacity(valueLength))
            {
                Overflowed = true;
                return;
            }

            value.Slice(0, valueLength).CopyTo(Buffer.Slice(Length));
            Length += valueLength;
            AddNullTerminator();
        }

        internal void AppendInternal(byte value)
        {
            if (Overflowed) return;

            if (!HasAdditionalCapacity(1))
            {
                Overflowed = true;
                return;
            }

            Buffer[Length] = value;
            Length++;
            AddNullTerminator();
        }

        internal void AppendFormatInternal(byte value, char format = 'G', byte precision = 255) =>
            AppendFormatUInt64(value, format, precision);

        internal void AppendFormatInternal(sbyte value, char format = 'G', byte precision = 255) =>
            AppendFormatInt64(value, 0xff, format, precision);

        internal void AppendFormatInternal(ushort value, char format = 'G', byte precision = 255) =>
            AppendFormatUInt64(value, format, precision);

        internal void AppendFormatInternal(short value, char format = 'G', byte precision = 255) =>
            AppendFormatInt64(value, 0xffff, format, precision);

        internal void AppendFormatInternal(uint value, char format = 'G', byte precision = 255) =>
            AppendFormatUInt64(value, format, precision);

        internal void AppendFormatInternal(int value, char format = 'G', byte precision = 255) =>
            AppendFormatInt64(value, 0xffffff, format, precision);

        internal void AppendFormatInternal(ulong value, char format = 'G', byte precision = 255) =>
            AppendFormatUInt64(value, format, precision);

        internal void AppendFormatInternal(long value, char format = 'G', byte precision = 255) =>
            AppendFormatInt64(value, 0xffffffff, format, precision);

        internal void AppendFormatInternal(float value, char format = 'G', byte precision = 255) =>
            AppendFormatFloat(value, format, precision);

        internal void AppendFormatInternal(double value, char format = 'G', byte precision = 255) =>
            AppendFormatDouble(value, format, precision);

        private readonly bool HasCapacity(int requiredCapacity)
        {
            return requiredCapacity <= Capacity;
        }

        private readonly bool HasAdditionalCapacity(int requiredAdditionalCapacity)
        {
            return HasCapacity(Length + requiredAdditionalCapacity);
        }

        private void AddNullTerminator()
        {
            Buffer[Length] = 0;
        }

        private readonly void ThrowIfBufferLengthIsZero()
        {
            if (Buffer.Length == 0) throw new ArgumentException("Buffer length must be greater than 0.");
        }

        private void AppendFormatInt64(long value, ulong mask, char format, byte precision)
        {
            if (Overflowed) return;

            // Remove possible sign extension if needed
            if (mask == 'x' | mask == 'X')
            {
                value &= (long)mask;
            }

            // Exclude the null terminator from the buffer because Utf8Formatter doesn't handle it
            Span<byte> availableBuffer = Buffer.Slice(Length, Capacity - Length);

            bool bufferLargeEnough = Utf8Formatter.TryFormat(value, availableBuffer, out int bytesWritten,
                new StandardFormat(format, precision));

            if (!bufferLargeEnough)
            {
                Overflowed = true;
                return;
            }

            Length += bytesWritten;
            AddNullTerminator();
        }

        private void AppendFormatUInt64(ulong value, char format, byte precision)
        {
            if (Overflowed) return;

            // Exclude the null terminator from the buffer because Utf8Formatter doesn't handle it
            Span<byte> availableBuffer = Buffer.Slice(Length, Capacity - Length);

            bool bufferLargeEnough = Utf8Formatter.TryFormat(value, availableBuffer, out int bytesWritten,
                new StandardFormat(format, precision));

            if (!bufferLargeEnough)
            {
                Overflowed = true;
                return;
            }

            Length += bytesWritten;
            AddNullTerminator();
        }

        private void AppendFormatFloat(float value, char format, byte precision)
        {
            if (Overflowed) return;

            // Exclude the null terminator from the buffer because Utf8Formatter doesn't handle it
            Span<byte> availableBuffer = Buffer.Slice(Length, Capacity - Length);

            bool bufferLargeEnough = Utf8Formatter.TryFormat(value, availableBuffer, out int bytesWritten,
                new StandardFormat(format, precision));

            if (!bufferLargeEnough)
            {
                Overflowed = true;
                return;
            }

            Length += bytesWritten;
            AddNullTerminator();
        }

        private void AppendFormatDouble(double value, char format, byte precision)
        {
            if (Overflowed) return;

            // Exclude the null terminator from the buffer because Utf8Formatter doesn't handle it
            Span<byte> availableBuffer = Buffer.Slice(Length, Capacity - Length);

            bool bufferLargeEnough = Utf8Formatter.TryFormat(value, availableBuffer, out int bytesWritten,
                new StandardFormat(format, precision));

            if (!bufferLargeEnough)
            {
                Overflowed = true;
                return;
            }

            Length += bytesWritten;
            AddNullTerminator();
        }

        public override readonly string ToString() => StringUtils.Utf8ZToString(Buffer);
    }

    public static class U8StringBuilderExtensions
    {
        public static ref U8StringBuilder Append(this ref U8StringBuilder sb, ReadOnlySpan<byte> value)
        {
            sb.AppendInternal(value);
            return ref sb;
        }

        public static ref U8StringBuilder Append(this ref U8StringBuilder sb, byte value)
        {
            sb.AppendInternal(value);
            return ref sb;
        }

        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, byte value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, sbyte value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, ushort value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, short value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, uint value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, int value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, ulong value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, long value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, float value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, double value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }
    }
}
