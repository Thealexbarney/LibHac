using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;

namespace LibHac.Util
{
    public enum CharacterEncodingResult
    {
        Success = 0,
        InsufficientLength = 1,
        InvalidFormat = 2
    }

    public static class CharacterEncoding
    {
        private static ReadOnlySpan<sbyte> Utf8NBytesInnerTable => new sbyte[]
        {
            -1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 7, 8
        };

        private static ReadOnlySpan<sbyte> Utf8NBytesTable => Utf8NBytesInnerTable.Slice(1);

        private static CharacterEncodingResult ConvertStringUtf8ToUtf16Impl(out int codeUnitsWritten,
            out int codeUnitsRead, Span<ushort> destination, ReadOnlySpan<byte> source)
        {
            if (source.Length == 0)
            {
                codeUnitsWritten = 0;
                codeUnitsRead = 0;
                return CharacterEncodingResult.Success;
            }

            ReadOnlySpan<byte> src = source;
            Span<ushort> dst = destination;

            while (src.Length > 0)
            {
                int codePointBytes = Utf8NBytesTable[src[0]];

                if (src.Length < codePointBytes)
                    goto ReturnInvalidFormat;

                if (dst.Length == 0)
                    goto ReturnInsufficientLength;

                uint codePoint;

                switch (codePointBytes)
                {
                    case 1:
                        dst[0] = src[0];
                        src = src.Slice(1);
                        dst = dst.Slice(1);
                        break;

                    case 2:
                        // Check if the encoding is overlong
                        if ((src[0] & 0x1E) == 0)
                            goto ReturnInvalidFormat;

                        if ((src[1] & 0xC0) != 0x80)
                            goto ReturnInvalidFormat;

                        codePoint = ((src[0] & 0x1Fu) << 6) |
                                    ((src[1] & 0x3Fu) << 0);

                        dst[0] = (ushort)codePoint;
                        src = src.Slice(2);
                        dst = dst.Slice(1);
                        break;

                    case 3:
                        if ((src[1] & 0xC0) != 0x80)
                            goto ReturnInvalidFormat;

                        if ((src[2] & 0xC0) != 0x80)
                            goto ReturnInvalidFormat;

                        codePoint = ((src[0] & 0xFu) << 12) |
                                    ((src[1] & 0x3Fu) << 6) |
                                    ((src[2] & 0x3Fu) << 0);

                        // Check if the encoding is overlong
                        if ((codePoint & 0xF800) == 0)
                            goto ReturnInvalidFormat;

                        // Check if the code point is in the range reserved for UTF-16 surrogates
                        if ((codePoint & 0xF800) == 0xD800)
                            goto ReturnInvalidFormat;

                        dst[0] = (ushort)codePoint;
                        src = src.Slice(3);
                        dst = dst.Slice(1);
                        break;

                    case 4:
                        if ((src[1] & 0xC0) != 0x80)
                            goto ReturnInvalidFormat;

                        if ((src[2] & 0xC0) != 0x80)
                            goto ReturnInvalidFormat;

                        if ((src[3] & 0xC0) != 0x80)
                            goto ReturnInvalidFormat;

                        codePoint = ((src[0] & 7u) << 18) |
                                    ((src[1] & 0x3Fu) << 12) |
                                    ((src[2] & 0x3Fu) << 6) |
                                    ((src[3] & 0x3Fu) << 0);

                        // Check if the code point is outside the range of valid code points
                        if (codePoint < 0x10000 || codePoint >= 0x110000)
                            goto ReturnInvalidFormat;

                        // Make sure we have enough space left in the destination
                        if (dst.Length == 1)
                            goto ReturnInsufficientLength;

                        ushort highSurrogate = (ushort)((codePoint - 0x10000) / 0x400 + 0xD800);
                        ushort lowSurrogate = (ushort)((codePoint - 0x10000) % 0x400 + 0xDC00);

                        dst[0] = highSurrogate;
                        dst[1] = lowSurrogate;
                        src = src.Slice(4);
                        dst = dst.Slice(2);
                        break;

                    default:
                        goto ReturnInvalidFormat;
                }
            }

            codeUnitsWritten = destination.Length - dst.Length;
            codeUnitsRead = source.Length - src.Length;
            return CharacterEncodingResult.Success;

            ReturnInvalidFormat:
            codeUnitsWritten = destination.Length - dst.Length;
            codeUnitsRead = source.Length - src.Length;
            return CharacterEncodingResult.InvalidFormat;

            ReturnInsufficientLength:
            codeUnitsWritten = destination.Length - dst.Length;
            codeUnitsRead = source.Length - src.Length;
            return CharacterEncodingResult.InsufficientLength;
        }

        private static CharacterEncodingResult ConvertStringUtf16ToUtf8Impl(out int codeUnitsWritten,
            out int codeUnitsRead, Span<byte> destination, ReadOnlySpan<ushort> source)
        {
            if (source.Length == 0)
            {
                codeUnitsWritten = 0;
                codeUnitsRead = 0;
                return CharacterEncodingResult.Success;
            }

            ReadOnlySpan<ushort> src = source;
            Span<byte> dst = destination;

            while (src.Length > 0)
            {
                ushort codeUnit1 = src[0];

                if (codeUnit1 < 0x80)
                {
                    if (dst.Length < 1)
                        goto ReturnInsufficientLength;

                    dst[0] = (byte)codeUnit1;
                    src = src.Slice(1);
                    dst = dst.Slice(1);
                }
                else if ((codeUnit1 & 0xF800) == 0)
                {
                    if (dst.Length < 2)
                        goto ReturnInsufficientLength;

                    dst[0] = (byte)(0xC0 | (codeUnit1 >> 6) & 0x1F);
                    dst[1] = (byte)(0x80 | codeUnit1 & 0x3F);
                    src = src.Slice(1);
                    dst = dst.Slice(2);
                }
                else if (codeUnit1 < 0xD800 || codeUnit1 >= 0xE000)
                {
                    if (dst.Length < 3)
                        goto ReturnInsufficientLength;

                    dst[0] = (byte)(0xE0 | (codeUnit1 >> 12) & 0xF);
                    dst[1] = (byte)(0x80 | (codeUnit1 >> 6) & 0x3F);
                    dst[2] = (byte)(0x80 | codeUnit1 & 0x3F);
                    src = src.Slice(1);
                    dst = dst.Slice(3);
                }
                else
                {
                    uint utf32;

                    if (source.Length == 1)
                    {
                        // If the code unit is a high surrogate
                        if ((codeUnit1 & 0xF800) == 0xD800 && (codeUnit1 & 0x400) == 0)
                        {
                            if (dst.Length < 1)
                                goto ReturnInsufficientLength;

                            // We have the first half of a surrogate pair. Get the code point as if the low surrogate
                            // were 0xDC00, effectively ignoring it. The first byte of the UTF-8-encoded code point does not
                            // ever depend on the low surrogate, so we can write what the first byte would be.
                            // The second byte doesn't ever depend on the low surrogate either, so I don't know why Nintendo
                            // doesn't write that one too. I'll admit I'm not even sure why they write the first byte. This
                            // reasoning is simply my best guess.
                            const int codeUnit2 = 0xDC00;
                            utf32 = ((codeUnit1 - 0xD800u) << 10) + codeUnit2 + 0x2400;

                            dst[0] = (byte)(0xF0 | (utf32 >> 18));
                            dst = dst.Slice(1);
                        }

                        goto ReturnInvalidFormat;
                    }

                    int codeUnitsUsed = ConvertCharacterUtf16ToUtf32(out utf32, codeUnit1, src[1]);

                    if (codeUnitsUsed < 0)
                    {
                        if (codeUnitsUsed == -2 && dst.Length > 0)
                        {
                            // We have an unpaired surrogate. Output the first UTF-8 code unit of the code point
                            // ConvertCharacterUtf16ToUtf32 gave us. Nintendo's reason for doing this is unclear.
                            dst[0] = (byte)(0xF0 | (utf32 >> 18));
                            dst = dst.Slice(1);
                        }

                        goto ReturnInvalidFormat;
                    }

                    if (dst.Length < 4)
                        goto ReturnInsufficientLength;

                    dst[0] = (byte)(0xF0 | (utf32 >> 18));
                    dst[1] = (byte)(0x80 | (utf32 >> 12) & 0x3F);
                    dst[2] = (byte)(0x80 | (utf32 >> 6) & 0x3F);
                    dst[3] = (byte)(0x80 | (utf32 >> 0) & 0x3F);
                    src = src.Slice(2);
                    dst = dst.Slice(4);
                }
            }

            codeUnitsWritten = destination.Length - dst.Length;
            codeUnitsRead = source.Length - src.Length;
            return CharacterEncodingResult.Success;

            ReturnInvalidFormat:
            codeUnitsWritten = destination.Length - dst.Length;
            codeUnitsRead = source.Length - src.Length;
            return CharacterEncodingResult.InvalidFormat;

            ReturnInsufficientLength:
            codeUnitsWritten = destination.Length - dst.Length;
            codeUnitsRead = source.Length - src.Length;
            return CharacterEncodingResult.InsufficientLength;
        }

        private static CharacterEncodingResult ConvertStringUtf8ToUtf32Impl(out int codeUnitsWritten,
            out int codeUnitsRead, Span<uint> destination, ReadOnlySpan<byte> source)
        {
            if (source.Length == 0)
            {
                codeUnitsWritten = 0;
                codeUnitsRead = 0;
                return CharacterEncodingResult.Success;
            }

            ReadOnlySpan<byte> src = source;
            Span<uint> dst = destination;

            while (src.Length > 0)
            {
                int codePointBytes = Utf8NBytesTable[src[0]];

                if (src.Length < codePointBytes)
                    goto ReturnInvalidFormat;

                if (dst.Length == 0)
                    goto ReturnInsufficientLength;

                uint codePoint;

                switch (codePointBytes)
                {
                    case 1:
                        dst[0] = src[0];
                        src = src.Slice(1);
                        dst = dst.Slice(1);
                        break;

                    case 2:
                        // Check if the encoding is overlong
                        if ((src[0] & 0x1E) == 0)
                            goto ReturnInvalidFormat;

                        if (Utf8NBytesTable[src[1]] != 0)
                            goto ReturnInvalidFormat;

                        codePoint = ((src[0] & 0x1Fu) << 6) |
                                    ((src[1] & 0x3Fu) << 0);

                        dst[0] = codePoint;
                        src = src.Slice(2);
                        dst = dst.Slice(1);
                        break;

                    case 3:
                        if (Utf8NBytesTable[src[1]] != 0)
                            goto ReturnInvalidFormat;

                        if (Utf8NBytesTable[src[2]] != 0)
                            goto ReturnInvalidFormat;

                        codePoint = ((src[0] & 0xFu) << 12) |
                                    ((src[1] & 0x3Fu) << 6) |
                                    ((src[2] & 0x3Fu) << 0);

                        // Check if the encoding is overlong
                        if ((codePoint & 0xF800) == 0)
                            goto ReturnInvalidFormat;

                        // Check if the code point is in the range reserved for UTF-16 surrogates
                        if ((codePoint & 0xF800) == 0xD800)
                            goto ReturnInvalidFormat;

                        dst[0] = codePoint;
                        src = src.Slice(3);
                        dst = dst.Slice(1);
                        break;

                    case 4:
                        if (Utf8NBytesTable[src[1]] != 0)
                            goto ReturnInvalidFormat;

                        if (Utf8NBytesTable[src[2]] != 0)
                            goto ReturnInvalidFormat;

                        if (Utf8NBytesTable[src[3]] != 0)
                            goto ReturnInvalidFormat;

                        codePoint = ((src[0] & 7u) << 18) |
                                    ((src[1] & 0x3Fu) << 12) |
                                    ((src[2] & 0x3Fu) << 6) |
                                    ((src[3] & 0x3Fu) << 0);

                        // Check if the code point is outside the range of valid code points
                        if (codePoint < 0x10000 || codePoint >= 0x110000)
                            goto ReturnInvalidFormat;

                        dst[0] = codePoint;
                        src = src.Slice(4);
                        dst = dst.Slice(1);
                        break;

                    default:
                        goto ReturnInvalidFormat;
                }
            }

            codeUnitsWritten = destination.Length - dst.Length;
            codeUnitsRead = source.Length - src.Length;
            return CharacterEncodingResult.Success;

            ReturnInvalidFormat:
            codeUnitsWritten = destination.Length - dst.Length;
            codeUnitsRead = source.Length - src.Length;
            return CharacterEncodingResult.InvalidFormat;

            ReturnInsufficientLength:
            codeUnitsWritten = destination.Length - dst.Length;
            codeUnitsRead = source.Length - src.Length;
            return CharacterEncodingResult.InsufficientLength;
        }

        private static CharacterEncodingResult ConvertStringUtf32ToUtf8Impl(out int codeUnitsWritten,
            out int codeUnitsRead, Span<byte> destination, ReadOnlySpan<uint> source)
        {
            if (source.Length == 0)
            {
                codeUnitsWritten = 0;
                codeUnitsRead = 0;
                return CharacterEncodingResult.Success;
            }

            ReadOnlySpan<uint> src = source;
            Span<byte> dst = destination;

            while ((uint)src.Length > 0)
            {
                uint codePoint = src[0];

                if (codePoint < 0x80)
                {
                    if (dst.Length < 1)
                        goto ReturnInsufficientLength;

                    dst[0] = (byte)codePoint;
                    dst = dst.Slice(1);
                }
                else if (codePoint < 0x800)
                {
                    if (dst.Length < 2)
                        goto ReturnInsufficientLength;

                    dst[0] = (byte)(0xC0 | codePoint >> 6);
                    dst[1] = (byte)(0x80 | codePoint & 0x3F);
                    dst = dst.Slice(2);
                }
                else if (codePoint < 0x10000)
                {
                    if (codePoint >= 0xD800 && codePoint <= 0xDFFF)
                        goto ReturnInvalidFormat;

                    if (dst.Length < 3)
                        goto ReturnInsufficientLength;

                    dst[0] = (byte)(0xE0 | (codePoint >> 12) & 0xF);
                    dst[1] = (byte)(0x80 | (codePoint >> 6) & 0x3F);
                    dst[2] = (byte)(0x80 | (codePoint >> 0) & 0x3F);
                    dst = dst.Slice(3);
                }
                else if (codePoint < 0x110000)
                {
                    if (dst.Length < 4)
                        goto ReturnInsufficientLength;

                    dst[0] = (byte)(0xF0 | codePoint >> 18);
                    dst[1] = (byte)(0x80 | (codePoint >> 12) & 0x3F);
                    dst[2] = (byte)(0x80 | (codePoint >> 6) & 0x3F);
                    dst[3] = (byte)(0x80 | (codePoint >> 0) & 0x3F);
                    dst = dst.Slice(4);
                }
                else
                {
                    goto ReturnInvalidFormat;
                }

                src = src.Slice(1);
            }

            codeUnitsWritten = destination.Length - dst.Length;
            codeUnitsRead = source.Length - src.Length;
            return CharacterEncodingResult.Success;

            ReturnInvalidFormat:
            codeUnitsWritten = destination.Length - dst.Length;
            codeUnitsRead = source.Length - src.Length;
            return CharacterEncodingResult.InvalidFormat;

            ReturnInsufficientLength:
            codeUnitsWritten = destination.Length - dst.Length;
            codeUnitsRead = source.Length - src.Length;
            return CharacterEncodingResult.InsufficientLength;
        }

        private static int ConvertCharacterUtf16ToUtf32(out uint outUtf32, ushort codeUnit1, ushort codeUnit2)
        {
            UnsafeHelpers.SkipParamInit(out outUtf32);

            // If the first code unit isn't a surrogate, simply copy it to the output
            if ((codeUnit1 & 0xF800) != 0xD800)
            {
                outUtf32 = codeUnit1;
                return 1;
            }

            // Make sure the high surrogate isn't in the range of low surrogate values
            if ((codeUnit1 & 0x400) != 0)
                return -1;

            // We still output a code point value if we have an unpaired high surrogate.
            // Nintendo's reason for doing this is unclear.
            outUtf32 = ((codeUnit1 - 0xD800u) << 10) + codeUnit2 + 0x2400;

            // Make sure the low surrogate is in the range of low surrogate values
            if ((codeUnit2 & 0xFC00) != 0xDC00)
                return -2;

            return 2;
        }

        private static int GetLengthOfUtf16(ReadOnlySpan<ushort> source)
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == 0)
                    return i;
            }

            return source.Length;
        }

        private static int GetLengthOfUtf32(ReadOnlySpan<uint> source)
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == 0)
                    return i;
            }

            return source.Length;
        }

        public static CharacterEncodingResult ConvertStringUtf8ToUtf16Native(Span<ushort> destination,
            ReadOnlySpan<byte> source, int sourceLength)
        {
            Assert.SdkRequires(0 <= sourceLength, $"{nameof(sourceLength)} must not be negative.");
            Assert.SdkRequires(sourceLength <= source.Length);

            return ConvertStringUtf8ToUtf16Impl(out _, out _, destination, source.Slice(0, sourceLength));
        }

        public static CharacterEncodingResult ConvertStringUtf8ToUtf16Native(Span<char> destination,
            ReadOnlySpan<byte> source, int sourceLength)
        {
            return ConvertStringUtf8ToUtf16Native(MemoryMarshal.Cast<char, ushort>(destination), source, sourceLength);
        }

        public static CharacterEncodingResult ConvertStringUtf8ToUtf16Native(Span<ushort> destination,
            ReadOnlySpan<byte> source)
        {
            int length = StringUtils.GetLength(source);

            Assert.SdkAssert(0 <= length);

            CharacterEncodingResult result = ConvertStringUtf8ToUtf16Impl(out int writtenCount, out _,
                destination.Slice(0, destination.Length - 1), source.Slice(0, length));

            if (result == CharacterEncodingResult.Success)
                destination[writtenCount] = 0;

            return result;
        }

        public static CharacterEncodingResult ConvertStringUtf8ToUtf16Native(Span<char> destination,
            ReadOnlySpan<byte> source)
        {
            return ConvertStringUtf8ToUtf16Native(MemoryMarshal.Cast<char, ushort>(destination), source);
        }

        public static CharacterEncodingResult ConvertStringUtf16NativeToUtf8(Span<byte> destination,
            ReadOnlySpan<ushort> source, int sourceLength)
        {
            Assert.SdkRequires(0 <= sourceLength, $"{nameof(sourceLength)} must not be negative.");
            Assert.SdkRequires(sourceLength <= source.Length);

            return ConvertStringUtf16ToUtf8Impl(out _, out _, destination, source.Slice(0, sourceLength));
        }

        public static CharacterEncodingResult ConvertStringUtf16NativeToUtf8(Span<byte> destination,
            ReadOnlySpan<char> source, int sourceLength)
        {
            return ConvertStringUtf16NativeToUtf8(destination, MemoryMarshal.Cast<char, ushort>(source), sourceLength);
        }

        public static CharacterEncodingResult ConvertStringUtf16NativeToUtf8(Span<byte> destination,
            ReadOnlySpan<ushort> source)
        {
            int length = GetLengthOfUtf16(source);

            Assert.SdkAssert(0 <= length);

            CharacterEncodingResult result = ConvertStringUtf16ToUtf8Impl(out int writtenCount, out _,
                destination.Slice(0, destination.Length - 1), source.Slice(0, length));

            if (result == CharacterEncodingResult.Success)
                destination[writtenCount] = 0;

            return result;
        }

        public static CharacterEncodingResult ConvertStringUtf16NativeToUtf8(Span<byte> destination,
            ReadOnlySpan<char> source)
        {
            return ConvertStringUtf16NativeToUtf8(destination, MemoryMarshal.Cast<char, ushort>(source));
        }

        public static CharacterEncodingResult GetLengthOfConvertedStringUtf8ToUtf16Native(out int length,
            ReadOnlySpan<byte> source, int sourceLength)
        {
            UnsafeHelpers.SkipParamInit(out length);
            Span<ushort> buffer = stackalloc ushort[0x20];

            Assert.SdkRequires(0 <= sourceLength, $"{nameof(sourceLength)} must not be negative.");
            Assert.SdkRequires(sourceLength <= source.Length);

            int totalLength = 0;
            source = source.Slice(0, sourceLength);

            while (source.Length > 0)
            {
                CharacterEncodingResult result =
                    ConvertStringUtf8ToUtf16Impl(out int writtenCount, out int readCount, buffer, source);

                if (result == CharacterEncodingResult.InvalidFormat)
                    return CharacterEncodingResult.InvalidFormat;

                totalLength += writtenCount;
                source = source.Slice(readCount);
            }

            Assert.SdkAssert(0 <= totalLength);

            length = totalLength;
            return CharacterEncodingResult.Success;
        }

        public static CharacterEncodingResult GetLengthOfConvertedStringUtf8ToUtf16Native(out int length,
            ReadOnlySpan<byte> source)
        {
            int sourceLength = StringUtils.GetLength(source);

            Assert.SdkAssert(0 <= sourceLength);

            return GetLengthOfConvertedStringUtf8ToUtf16Native(out length, source, sourceLength);
        }

        public static CharacterEncodingResult GetLengthOfConvertedStringUtf16NativeToUtf8(out int length,
            ReadOnlySpan<ushort> source, int sourceLength)
        {
            UnsafeHelpers.SkipParamInit(out length);
            Span<byte> buffer = stackalloc byte[0x20];

            Assert.SdkRequires(0 <= sourceLength, $"{nameof(sourceLength)} must not be negative.");
            Assert.SdkRequires(sourceLength <= source.Length);

            int totalLength = 0;
            source = source.Slice(0, sourceLength);

            while (source.Length > 0)
            {
                CharacterEncodingResult result =
                    ConvertStringUtf16ToUtf8Impl(out int writtenCount, out int readCount, buffer, source);

                if (result == CharacterEncodingResult.InvalidFormat)
                    return CharacterEncodingResult.InvalidFormat;

                totalLength += writtenCount;
                source = source.Slice(readCount);
            }

            Assert.SdkAssert(0 <= totalLength);

            length = totalLength;
            return CharacterEncodingResult.Success;
        }

        public static CharacterEncodingResult GetLengthOfConvertedStringUtf16NativeToUtf8(out int length,
            ReadOnlySpan<char> source, int sourceLength)
        {
            return GetLengthOfConvertedStringUtf16NativeToUtf8(out length, MemoryMarshal.Cast<char, ushort>(source),
                sourceLength);
        }

        public static CharacterEncodingResult GetLengthOfConvertedStringUtf16NativeToUtf8(out int length,
            ReadOnlySpan<ushort> source)
        {
            int sourceLength = GetLengthOfUtf16(source);

            Assert.SdkAssert(0 <= sourceLength);

            return GetLengthOfConvertedStringUtf16NativeToUtf8(out length, source, sourceLength);
        }

        public static CharacterEncodingResult GetLengthOfConvertedStringUtf16NativeToUtf8(out int length,
            ReadOnlySpan<char> source)
        {
            return GetLengthOfConvertedStringUtf16NativeToUtf8(out length, MemoryMarshal.Cast<char, ushort>(source));
        }

        public static CharacterEncodingResult ConvertStringUtf8ToUtf32(Span<uint> destination,
            ReadOnlySpan<byte> source, int sourceLength)
        {
            Assert.SdkRequires(0 <= sourceLength, $"{nameof(sourceLength)} must not be negative.");
            Assert.SdkRequires(sourceLength <= source.Length);

            return ConvertStringUtf8ToUtf32Impl(out _, out _, destination, source.Slice(0, sourceLength));
        }

        public static CharacterEncodingResult ConvertStringUtf8ToUtf32(Span<uint> destination,
            ReadOnlySpan<byte> source)
        {
            int sourceLength = StringUtils.GetLength(source);

            Assert.SdkAssert(0 <= sourceLength);

            CharacterEncodingResult result = ConvertStringUtf8ToUtf32Impl(out int writtenCount, out _,
                destination.Slice(0, destination.Length - 1), source.Slice(0, sourceLength));

            if (result == CharacterEncodingResult.Success)
                destination[writtenCount] = 0;

            return result;
        }

        public static CharacterEncodingResult ConvertStringUtf32ToUtf8(Span<byte> destination,
            ReadOnlySpan<uint> source, int sourceLength)
        {
            Assert.SdkRequires(0 <= sourceLength, $"{nameof(sourceLength)} must not be negative.");
            Assert.SdkRequires(sourceLength <= source.Length);

            return ConvertStringUtf32ToUtf8Impl(out _, out _, destination, source.Slice(0, sourceLength));
        }

        public static CharacterEncodingResult ConvertStringUtf32ToUtf8(Span<byte> destination,
            ReadOnlySpan<uint> source)
        {
            int sourceLength = GetLengthOfUtf32(source);

            Assert.SdkAssert(0 <= sourceLength);

            CharacterEncodingResult result = ConvertStringUtf32ToUtf8Impl(out int writtenCount, out _,
                destination.Slice(0, destination.Length - 1), source.Slice(0, sourceLength));

            if (result == CharacterEncodingResult.Success)
                destination[writtenCount] = 0;

            return result;
        }

        public static CharacterEncodingResult GetLengthOfConvertedStringUtf8ToUtf32(out int length,
            ReadOnlySpan<byte> source, int sourceLength)
        {
            UnsafeHelpers.SkipParamInit(out length);
            Span<uint> buffer = stackalloc uint[0x20];

            Assert.SdkRequires(0 <= sourceLength, $"{nameof(sourceLength)} must not be negative.");
            Assert.SdkRequires(sourceLength <= source.Length);

            int totalLength = 0;
            source = source.Slice(0, sourceLength);

            while (source.Length > 0)
            {
                CharacterEncodingResult result =
                    ConvertStringUtf8ToUtf32Impl(out int writtenCount, out int readCount, buffer, source);

                if (result == CharacterEncodingResult.InvalidFormat)
                    return CharacterEncodingResult.InvalidFormat;

                totalLength += writtenCount;
                source = source.Slice(readCount);
            }

            Assert.SdkAssert(0 <= totalLength);

            length = totalLength;
            return CharacterEncodingResult.Success;
        }

        public static CharacterEncodingResult GetLengthOfConvertedStringUtf8ToUtf32(out int length,
            ReadOnlySpan<byte> source)
        {
            int sourceLength = StringUtils.GetLength(source);

            Assert.SdkAssert(0 <= sourceLength);

            return GetLengthOfConvertedStringUtf8ToUtf32(out length, source, sourceLength);
        }

        public static CharacterEncodingResult GetLengthOfConvertedStringUtf32ToUtf8(out int length,
            ReadOnlySpan<uint> source, int sourceLength)
        {
            UnsafeHelpers.SkipParamInit(out length);
            Span<byte> buffer = stackalloc byte[0x20];

            Assert.SdkRequires(0 <= sourceLength, $"{nameof(sourceLength)} must not be negative.");
            Assert.SdkRequires(sourceLength <= source.Length);

            int totalLength = 0;
            source = source.Slice(0, sourceLength);

            while (source.Length > 0)
            {
                CharacterEncodingResult result =
                    ConvertStringUtf32ToUtf8Impl(out int writtenCount, out int readCount, buffer, source);

                if (result == CharacterEncodingResult.InvalidFormat)
                    return CharacterEncodingResult.InvalidFormat;

                totalLength += writtenCount;
                source = source.Slice(readCount);
            }

            Assert.SdkAssert(0 <= totalLength);

            length = totalLength;
            return CharacterEncodingResult.Success;
        }

        public static CharacterEncodingResult GetLengthOfConvertedStringUtf32ToUtf8(out int length,
            ReadOnlySpan<uint> source)
        {
            int sourceLength = GetLengthOfUtf32(source);

            Assert.SdkAssert(0 <= sourceLength);

            return GetLengthOfConvertedStringUtf32ToUtf8(out length, source, sourceLength);
        }

        public static CharacterEncodingResult ConvertCharacterUtf8ToUtf16Native(Span<ushort> destination,
            ReadOnlySpan<byte> source)
        {
            if (destination.Length < 2)
                return CharacterEncodingResult.InsufficientLength;

            if (source.Length < 1)
                return CharacterEncodingResult.InvalidFormat;

            Span<byte> bufferSrc = stackalloc byte[5];
            Span<ushort> bufferDst = stackalloc ushort[3];

            bufferSrc[0] = source[0];
            bufferSrc[1] = 0;
            bufferSrc[2] = 0;
            bufferSrc[3] = 0;
            bufferSrc[4] = 0;

            // Read more code units if needed
            if (source[0] >= 0xC2 && source[0] < 0xE0)
            {
                if (source.Length < 2)
                    return CharacterEncodingResult.InvalidFormat;

                bufferSrc[1] = source[1];
            }
            else if (source[0] >= 0xE0 && source[0] < 0xF0)
            {
                if (source.Length < 3)
                    return CharacterEncodingResult.InvalidFormat;

                bufferSrc[1] = source[1];
                bufferSrc[2] = source[2];

            }
            else if (source[0] >= 0xF0 && source[0] < 0xF8)
            {
                if (source.Length < 4)
                    return CharacterEncodingResult.InvalidFormat;

                bufferSrc[1] = source[1];
                bufferSrc[2] = source[2];
                bufferSrc[3] = source[3];
            }

            bufferDst.Clear();

            CharacterEncodingResult result = ConvertStringUtf8ToUtf16Native(bufferDst, bufferSrc);
            destination[0] = bufferDst[0];
            destination[1] = bufferDst[1];

            return result;
        }

        public static CharacterEncodingResult ConvertCharacterUtf8ToUtf16Native(Span<char> destination,
            ReadOnlySpan<byte> source)
        {
            return ConvertCharacterUtf8ToUtf16Native(MemoryMarshal.Cast<char, ushort>(destination), source);
        }

        public static CharacterEncodingResult ConvertCharacterUtf16NativeToUtf8(Span<byte> destination,
            ReadOnlySpan<ushort> source)
        {
            if (destination.Length < 4)
                return CharacterEncodingResult.InsufficientLength;

            if (source.Length < 1)
                return CharacterEncodingResult.InvalidFormat;

            Span<ushort> bufferSrc = stackalloc ushort[3];
            Span<byte> bufferDst = stackalloc byte[5];

            bufferSrc[0] = source[0];
            bufferSrc[1] = 0;
            bufferSrc[2] = 0;

            // Read more code units if needed
            if (source[0] >= 0xD800 && source[0] < 0xE000)
            {
                if (source.Length < 2)
                    return CharacterEncodingResult.InvalidFormat;

                bufferSrc[1] = source[1];
            }

            bufferDst.Clear();

            CharacterEncodingResult result = ConvertStringUtf16NativeToUtf8(bufferDst, bufferSrc);
            destination[0] = bufferDst[0];
            destination[1] = bufferDst[1];
            destination[2] = bufferDst[2];
            destination[3] = bufferDst[3];

            return result;
        }

        public static CharacterEncodingResult ConvertCharacterUtf16NativeToUtf8(Span<byte> destination,
            ReadOnlySpan<char> source)
        {
            return ConvertCharacterUtf16NativeToUtf8(destination, MemoryMarshal.Cast<char, ushort>(source));
        }

        public static CharacterEncodingResult ConvertCharacterUtf8ToUtf32(out uint destination,
            ReadOnlySpan<byte> source)
        {
            UnsafeHelpers.SkipParamInit(out destination);

            if (source.Length < 1)
                return CharacterEncodingResult.InvalidFormat;

            switch (Utf8NBytesTable[source[0]])
            {
                case 1:
                    destination = source[0];
                    return CharacterEncodingResult.Success;

                case 2:
                    if (source.Length < 2) break;
                    if ((source[0] & 0x1E) == 0) break;
                    if (Utf8NBytesTable[source[1]] != 0) break;

                    destination = ((source[0] & 0x1Fu) << 6) | ((source[1] & 0x3Fu) << 0);
                    return CharacterEncodingResult.Success;

                case 3:
                    if (source.Length < 3) break;
                    if (Utf8NBytesTable[source[1]] != 0 || Utf8NBytesTable[source[2]] != 0) break;

                    uint codePoint3 = ((source[0] & 0xFu) << 12) | ((source[1] & 0x3Fu) << 6) | ((source[2] & 0x3Fu) << 0);

                    if ((codePoint3 & 0xF800) == 0 || (codePoint3 & 0xF800) == 0xD800)
                        break;

                    destination = codePoint3;
                    return CharacterEncodingResult.Success;

                case 4:
                    if (source.Length < 4) break;
                    if (Utf8NBytesTable[source[1]] != 0 || Utf8NBytesTable[source[2]] != 0 || Utf8NBytesTable[source[3]] != 0) break;

                    uint codePoint4 = ((source[0] & 7u) << 18) | ((source[1] & 0x3Fu) << 12) | ((source[2] & 0x3Fu) << 6) | ((source[3] & 0x3Fu) << 0);

                    if (codePoint4 < 0x10000 || codePoint4 >= 0x110000)
                        break;

                    destination = codePoint4;
                    return CharacterEncodingResult.Success;
            }

            return CharacterEncodingResult.InvalidFormat;
        }

        public static CharacterEncodingResult ConvertCharacterUtf32ToUtf8(Span<byte> destination, uint source)
        {
            if (destination.Length < 4)
                return CharacterEncodingResult.InsufficientLength;

            destination[0] = 0;
            destination[1] = 0;
            destination[2] = 0;
            destination[3] = 0;

            if (source < 0x80)
            {
                destination[0] = (byte)source;
            }
            else if (source < 0x800)
            {
                destination[0] = (byte)(0xC0 | source >> 6);
                destination[1] = (byte)(0x80 | (source & 0x3F));
            }
            else if (source < 0x10000)
            {
                if (source >= 0xD800 && source <= 0xDFFF)
                    return CharacterEncodingResult.InvalidFormat;

                destination[0] = (byte)(0xE0 | (source >> 12) & 0xF);
                destination[1] = (byte)(0x80 | (source >> 6) & 0x3F);
                destination[2] = (byte)(0x80 | (source >> 0) & 0x3F);

            }
            else if (source < 0x110000)
            {
                destination[0] = (byte)(0xF0 | (source >> 18));
                destination[1] = (byte)(0x80 | (source >> 12) & 0x3F);
                destination[2] = (byte)(0x80 | (source >> 6) & 0x3F);
                destination[3] = (byte)(0x80 | (source >> 0) & 0x3F);
            }
            else
            {
                return CharacterEncodingResult.InvalidFormat;
            }

            return CharacterEncodingResult.Success;
        }

        public static CharacterEncodingResult PickOutCharacterFromUtf8String(Span<byte> destinationChar,
            ref ReadOnlySpan<byte> source)
        {
            Assert.SdkRequires(destinationChar.Length >= 4);
            Assert.SdkRequires(source.Length >= 1);
            Assert.SdkRequires(source[0] != 0);

            ReadOnlySpan<byte> str = source;

            if (destinationChar.Length < 4)
                return CharacterEncodingResult.InsufficientLength;

            if (str.Length < 1)
                return CharacterEncodingResult.InvalidFormat;

            destinationChar[0] = 0;
            destinationChar[1] = 0;
            destinationChar[2] = 0;
            destinationChar[3] = 0;

            uint codePoint = str[0];

            switch (Utf8NBytesTable[(int)codePoint])
            {
                case 1:
                    destinationChar[0] = str[0];
                    source = str.Slice(1);
                    break;

                case 2:
                    if (str.Length < 2)
                        return CharacterEncodingResult.InvalidFormat;

                    if ((str[0] & 0x1E) == 0 || Utf8NBytesTable[str[1]] != 0)
                        return CharacterEncodingResult.InvalidFormat;

                    destinationChar[0] = str[0];
                    destinationChar[1] = str[1];
                    source = str.Slice(2);
                    break;

                case 3:
                    if (str.Length < 3)
                        return CharacterEncodingResult.InvalidFormat;

                    if (Utf8NBytesTable[str[1]] != 0 || Utf8NBytesTable[str[2]] != 0)
                        return CharacterEncodingResult.InvalidFormat;

                    codePoint = ((str[0] & 0xFu) << 12) |
                                ((str[1] & 0x3Fu) << 6) |
                                ((str[2] & 0x3Fu) << 0);

                    if ((codePoint & 0xF800) == 0 || (codePoint & 0xF800) == 0xD800)
                        return CharacterEncodingResult.InvalidFormat;

                    destinationChar[0] = str[0];
                    destinationChar[1] = str[1];
                    destinationChar[2] = str[2];
                    source = str.Slice(3);
                    break;

                case 4:
                    if (str.Length < 4)
                        return CharacterEncodingResult.InvalidFormat;

                    if (Utf8NBytesTable[str[1]] != 0 || Utf8NBytesTable[str[2]] != 0 || Utf8NBytesTable[str[3]] != 0)
                        return CharacterEncodingResult.InvalidFormat;

                    codePoint = ((str[0] & 7u) << 18) |
                                ((str[1] & 0x3Fu) << 12) |
                                ((str[2] & 0x3Fu) << 6) |
                                ((str[3] & 0x3Fu) << 0);

                    if (codePoint < 0x10000 || codePoint >= 0x110000)
                        return CharacterEncodingResult.InvalidFormat;

                    destinationChar[0] = str[0];
                    destinationChar[1] = str[1];
                    destinationChar[2] = str[2];
                    destinationChar[3] = str[3];
                    source = str.Slice(4);
                    break;

                default:
                    return CharacterEncodingResult.InvalidFormat;
            }

            return CharacterEncodingResult.Success;
        }
    }
}
