using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using LibHac.Diag;
using LibHac.Util;

namespace LibHac.Common
{
    [DebuggerDisplay("{ToString()}")]
    public ref struct U8StringBuilder
    {
        private const int NullTerminatorLength = 1;

        public Span<byte> Buffer { get; private set; }
        public int Length { get; private set; }
        public bool Overflowed { get; private set; }
        public bool AutoExpand { get; }

        private enum PadType : byte
        {
            None,
            Left,
            Right
        }

        private PadType PaddingType { get; set; }
        private byte PaddingCharacter { get; set; }
        private byte PaddedLength { get; set; }

        private byte[] _rentedArray;

        public readonly int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Buffer.Length - NullTerminatorLength;
        }

        public U8StringBuilder(Span<byte> buffer, bool autoExpand = false)
        {
            Buffer = buffer;
            Length = 0;
            Overflowed = false;
            AutoExpand = autoExpand;
            PaddingType = PadType.None;
            PaddingCharacter = 0;
            PaddedLength = 0;
            _rentedArray = null;

            if (autoExpand)
            {
                TryEnsureAdditionalCapacity(1);
            }
            else
            {
                ThrowIfBufferLengthIsZero();
            }

            AddNullTerminator();
        }

        public void Dispose()
        {
            byte[] toReturn = _rentedArray;
            this = default;
            if (toReturn is not null)
            {
                ArrayPool<byte>.Shared.Return(_rentedArray);
            }
        }

        // These functions are internal so they can be called by the extension methods
        // in U8StringBuilderExtensions. It's not an ideal setup, but it allows append
        // calls to be chained without accidentally creating a copy of the U8StringBuilder.
        internal void AppendInternal(ReadOnlySpan<byte> value)
        {
            // Once in the Overflowed state, nothing else is written to the buffer.
            if (Overflowed) return;

            int valueLength = StringUtils.GetLength(value);

            // If needed, expand the buffer size if allowed, or set the Overflowed state if not.
            if (!TryEnsureAdditionalCapacity(valueLength))
            {
                Overflowed = true;
                return;
            }

            // Because we know the length of the output string beforehand, we can write the
            // proper amount of padding bytes before writing the output string to the buffer.
            if (PaddingType == PadType.Left && valueLength < PaddedLength)
            {
                int paddingNeeded = PaddedLength - valueLength;
                Buffer.Slice(Length, paddingNeeded).Fill(PaddingCharacter);
                PaddingType = PadType.None;
                Length += paddingNeeded;
            }

            // Copy the string to the buffer and right pad if necessary.
            value.Slice(0, valueLength).CopyTo(Buffer.Slice(Length));
            PadOutput(valueLength);

            // Update the length and null-terminate the string.
            Length += valueLength;
            AddNullTerminator();
        }

        internal void AppendInternal(byte value)
        {
            if (Overflowed) return;

            if (!TryEnsureAdditionalCapacity(1))
            {
                Overflowed = true;
                return;
            }

            if (PaddingType == PadType.Left && 1 < PaddedLength)
            {
                int paddingNeeded = PaddedLength - 1;
                Buffer.Slice(Length, paddingNeeded).Fill(PaddingCharacter);
                PaddingType = PadType.None;
                Length += paddingNeeded;
            }

            Buffer[Length] = value;
            PadOutput(1);

            Length++;
            AddNullTerminator();
        }

        private void AppendFormatInt64(long value, ulong mask, char format, byte precision)
        {
            if (Overflowed) return;

            // Check if we have enough remaining buffer to fit the required padding.
            if (!TryEnsureAdditionalCapacity(0))
            {
                Overflowed = true;
                return;
            }

            // Remove possible sign extension if needed
            if (mask == 'x' || mask == 'X')
            {
                value &= (long)mask;
            }

            int bytesWritten;

            // If auto-expand is enabled, try to expand the buffer until it can hold the output.
            while (true)
            {
                // Exclude the null terminator from the buffer because Utf8Formatter doesn't handle it
                Span<byte> availableBuffer = Buffer.Slice(Length, Capacity - Length);

                bool bufferLargeEnough = Utf8Formatter.TryFormat(value, availableBuffer, out bytesWritten,
                    new StandardFormat(format, precision));

                // Continue if the buffer is large enough.
                if (bufferLargeEnough)
                    break;

                // We can't expand the buffer. Mark the string builder as Overflowed
                if (!AutoExpand)
                {
                    Overflowed = true;
                    return;
                }

                // Grow the buffer and try again. The buffer size will at least double each time it grows,
                // so asking for 0x10 additional bytes actually gets us more than that.
                Grow(availableBuffer.Length + 0x10);
                Assert.SdkGreater(Capacity, Length + availableBuffer.Length);
            }

            PadOutput(bytesWritten);

            Length += bytesWritten;
            AddNullTerminator();
        }

        private void AppendFormatUInt64(ulong value, char format, byte precision)
        {
            if (Overflowed) return;

            if (!TryEnsureAdditionalCapacity(0))
            {
                Overflowed = true;
                return;
            }

            int bytesWritten;

            while (true)
            {
                // Exclude the null terminator from the buffer because Utf8Formatter doesn't handle it
                Span<byte> availableBuffer = Buffer.Slice(Length, Capacity - Length);

                bool bufferLargeEnough = Utf8Formatter.TryFormat(value, availableBuffer, out bytesWritten,
                    new StandardFormat(format, precision));

                if (bufferLargeEnough)
                    break;

                if (!AutoExpand)
                {
                    Overflowed = true;
                    return;
                }

                Grow(availableBuffer.Length + 0x10);
                Assert.SdkGreater(Capacity, Length + availableBuffer.Length);
            }

            PadOutput(bytesWritten);

            Length += bytesWritten;
            AddNullTerminator();
        }

        private void AppendFormatFloat(float value, char format, byte precision)
        {
            if (Overflowed) return;

            if (!TryEnsureAdditionalCapacity(0))
            {
                Overflowed = true;
                return;
            }

            int bytesWritten;

            while (true)
            {
                // Exclude the null terminator from the buffer because Utf8Formatter doesn't handle it
                Span<byte> availableBuffer = Buffer.Slice(Length, Capacity - Length);

                bool bufferLargeEnough = Utf8Formatter.TryFormat(value, availableBuffer, out bytesWritten,
                    new StandardFormat(format, precision));

                if (bufferLargeEnough)
                    break;

                if (!AutoExpand)
                {
                    Overflowed = true;
                    return;
                }

                Grow(availableBuffer.Length + 0x10);
                Assert.SdkGreater(Capacity, Length + availableBuffer.Length);
            }

            PadOutput(bytesWritten);

            Length += bytesWritten;
            AddNullTerminator();
        }

        private void AppendFormatDouble(double value, char format, byte precision)
        {
            if (Overflowed) return;

            if (!TryEnsureAdditionalCapacity(0))
            {
                Overflowed = true;
                return;
            }

            int bytesWritten;

            while (true)
            {
                // Exclude the null terminator from the buffer because Utf8Formatter doesn't handle it
                Span<byte> availableBuffer = Buffer.Slice(Length, Capacity - Length);

                bool bufferLargeEnough = Utf8Formatter.TryFormat(value, availableBuffer, out bytesWritten,
                    new StandardFormat(format, precision));

                if (bufferLargeEnough)
                    break;

                if (!AutoExpand)
                {
                    Overflowed = true;
                    return;
                }

                Grow(availableBuffer.Length + 0x10);
                Assert.SdkGreater(Capacity, Length + availableBuffer.Length);
            }

            PadOutput(bytesWritten);

            Length += bytesWritten;
            AddNullTerminator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendFormatInternal(byte value, char format = 'G', byte precision = 255) =>
            AppendFormatUInt64(value, format, precision);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendFormatInternal(sbyte value, char format = 'G', byte precision = 255) =>
            AppendFormatInt64(value, 0xff, format, precision);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendFormatInternal(ushort value, char format = 'G', byte precision = 255) =>
            AppendFormatUInt64(value, format, precision);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendFormatInternal(short value, char format = 'G', byte precision = 255) =>
            AppendFormatInt64(value, 0xffff, format, precision);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendFormatInternal(uint value, char format = 'G', byte precision = 255) =>
            AppendFormatUInt64(value, format, precision);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendFormatInternal(int value, char format = 'G', byte precision = 255) =>
            AppendFormatInt64(value, 0xffffff, format, precision);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendFormatInternal(ulong value, char format = 'G', byte precision = 255) =>
            AppendFormatUInt64(value, format, precision);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendFormatInternal(long value, char format = 'G', byte precision = 255) =>
            AppendFormatInt64(value, 0xffffffff, format, precision);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendFormatInternal(float value, char format = 'G', byte precision = 255) =>
            AppendFormatFloat(value, format, precision);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendFormatInternal(double value, char format = 'G', byte precision = 255) =>
            AppendFormatDouble(value, format, precision);

        internal void PadLeftInternal(byte paddingCharacter, byte paddedLength)
        {
            PaddingType = PadType.Left;
            PaddingCharacter = paddingCharacter;
            PaddedLength = paddedLength;
        }

        internal void PadRightInternal(byte paddingCharacter, byte paddedLength)
        {
            PaddingType = PadType.Right;
            PaddingCharacter = paddingCharacter;
            PaddedLength = paddedLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly bool HasCapacity(int requiredCapacity)
        {
            return requiredCapacity <= Capacity;
        }

        private bool TryEnsureAdditionalCapacity(int requiredAdditionalCapacity)
        {
            int paddedLength = PaddingType != PadType.None ? PaddedLength : 0;
            int requiredAfterPadding = Math.Max(paddedLength, requiredAdditionalCapacity);
            bool hasCapacity = HasCapacity(Length + requiredAfterPadding);

            if (!hasCapacity && AutoExpand)
            {
                Grow(requiredAfterPadding);
                Assert.SdkAssert(HasCapacity(Length + requiredAfterPadding));
                hasCapacity = true;
            }

            return hasCapacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddNullTerminator()
        {
            Buffer[Length] = 0;
        }

        private void Grow(int requiredAdditionalCapacity)
        {
            byte[] poolArray =
                ArrayPool<byte>.Shared.Rent(Math.Max(Length + requiredAdditionalCapacity, Capacity * 2));

            Buffer.Slice(0, Length).CopyTo(poolArray);
            Buffer = poolArray;

            byte[] toReturn = _rentedArray;
            _rentedArray = poolArray;
            if (toReturn is not null)
            {
                ArrayPool<byte>.Shared.Return(toReturn);
            }
        }

        private readonly void ThrowIfBufferLengthIsZero()
        {
            if (Buffer.Length == 0) throw new ArgumentException("Buffer length must be greater than 0.");
        }

        /// <summary>
        /// Pads the output after being written to the buffer if necessary,
        /// resetting the padding type to <see cref="PadType.None"/> afterward.
        /// </summary>
        /// <param name="bytesWritten">The length of the unpadded output string.</param>
        /// <remarks>Because it involves moving the output string when left padding, this function is only used
        /// when the length of the output string isn't known before writing it to the buffer.</remarks>
        private void PadOutput(int bytesWritten)
        {
            if (PaddingType == PadType.Right && bytesWritten < PaddedLength)
            {
                // Fill the remaining bytes to the right with the padding character
                int paddingNeeded = PaddedLength - bytesWritten;
                Buffer.Slice(Length + bytesWritten, paddingNeeded).Fill(PaddingCharacter);
                PaddingType = PadType.None;
                Length += paddingNeeded;
            }
            else if (PaddingType == PadType.Left && bytesWritten < PaddedLength)
            {
                int paddingNeeded = PaddedLength - bytesWritten;

                // Move the printed bytes to the right to make room for the padding
                Span<byte> source = Buffer.Slice(Length, bytesWritten);
                Span<byte> dest = Buffer.Slice(Length + paddingNeeded, bytesWritten);
                source.CopyTo(dest);

                // Fill the leading bytes with the padding character
                Buffer.Slice(Length, paddingNeeded).Fill(PaddingCharacter);
                PaddingType = PadType.None;
                Length += paddingNeeded;
            }
        }

        public override readonly string ToString() => StringUtils.Utf8ZToString(Buffer);
    }

    public static class U8StringBuilderExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder Append(this ref U8StringBuilder sb, ReadOnlySpan<byte> value)
        {
            sb.AppendInternal(value);
            return ref sb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder Append(this ref U8StringBuilder sb, byte value)
        {
            sb.AppendInternal(value);
            return ref sb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, byte value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, sbyte value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, ushort value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, short value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, uint value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, int value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, ulong value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, long value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, float value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder AppendFormat(this ref U8StringBuilder sb, double value, char format = 'G',
            byte precision = 255)
        {
            sb.AppendFormatInternal(value, format, precision);
            return ref sb;
        }

        /// <summary>
        /// Set the parameters used to left-pad the next appended value.
        /// </summary>
        /// <param name="sb">The used string builder.</param>
        /// <param name="paddingCharacter">The character used to pad the output.</param>
        /// <param name="paddedLength">The minimum length of the output after padding.</param>
        /// <returns>The used string builder.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder PadLeft(this ref U8StringBuilder sb, byte paddingCharacter, byte paddedLength)
        {
            sb.PadLeftInternal(paddingCharacter, paddedLength);
            return ref sb;
        }

        /// <summary>
        /// Set the parameters used to right-pad the next appended value.
        /// </summary>
        /// <param name="sb">The used string builder.</param>
        /// <param name="paddingCharacter">The character used to pad the output.</param>
        /// <param name="paddedLength">The minimum length of the output after padding.</param>
        /// <returns>The used string builder.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref U8StringBuilder PadRight(this ref U8StringBuilder sb, byte paddingCharacter, byte paddedLength)
        {
            sb.PadRightInternal(paddingCharacter, paddedLength);
            return ref sb;
        }
    }
}
