// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LibHac.Util;
using Xunit;

namespace LibHac.Tests.Util
{
    public class CharacterEncodingTests
    {
        // Most of these tests are stolen from .NET's UTF-8 tests. Some of the comments in this file may
        // mention code paths and functions being tested in the .NET runtime that don't apply here as a result.

        // ReSharper disable InconsistentNaming UnusedMember.Local
        private const string X_UTF8 = "58"; // U+0058 LATIN CAPITAL LETTER X, 1 byte
        private const string X_UTF16 = "X";
        
        private const string Y_UTF8 = "59"; // U+0058 LATIN CAPITAL LETTER Y, 1 byte
        private const string Y_UTF16 = "Y";

        private const string Z_UTF8 = "5A"; // U+0058 LATIN CAPITAL LETTER Z, 1 byte
        private const string Z_UTF16 = "Z";

        private const string E_ACUTE_UTF8 = "C3A9"; // U+00E9 LATIN SMALL LETTER E WITH ACUTE, 2 bytes
        private const string E_ACUTE_UTF16 = "\u00E9";

        private const string EURO_SYMBOL_UTF8 = "E282AC"; // U+20AC EURO SIGN, 3 bytes
        private const string EURO_SYMBOL_UTF16 = "\u20AC";

        private const string REPLACEMENT_CHAR_UTF8 = "EFBFBD"; // U+FFFD REPLACEMENT CHAR, 3 bytes
        private const string REPLACEMENT_CHAR_UTF16 = "\uFFFD";

        private const string GRINNING_FACE_UTF8 = "F09F9880"; // U+1F600 GRINNING FACE, 4 bytes
        private const string GRINNING_FACE_UTF16 = "\U0001F600";

        private const string WOMAN_CARTWHEELING_MEDSKIN_UTF16 = "\U0001F938\U0001F3FD\u200D\u2640\uFE0F"; // U+1F938 U+1F3FD U+200D U+2640 U+FE0F WOMAN CARTWHEELING: MEDIUM SKIN TONE

        // All valid scalars [ U+0000 .. U+D7FF ] and [ U+E000 .. U+10FFFF ].
        private static readonly IEnumerable<Rune> s_allValidScalars = Enumerable.Range(0x0000, 0xD800).Concat(Enumerable.Range(0xE000, 0x110000 - 0xE000)).Select(value => new Rune(value));

        private static readonly ReadOnlyMemory<uint> s_allScalarsAsUtf32;
        private static readonly ReadOnlyMemory<char> s_allScalarsAsUtf16;
        private static readonly ReadOnlyMemory<byte> s_allScalarsAsUtf8;
        // ReSharper restore InconsistentNaming UnusedMember.Local

        static CharacterEncodingTests()
        {
            var allScalarsAsUtf32 = new List<uint>();
            var allScalarsAsUtf16 = new List<char>();
            var allScalarsAsUtf8 = new List<byte>();

            Span<byte> utf8 = stackalloc byte[4];
            Span<char> utf16 = stackalloc char[2];

            foreach (Rune rune in s_allValidScalars)
            {
                int utf8Length = ToUtf8(rune, utf8);
                int utf16Length = ToUtf16(rune, utf16);

                allScalarsAsUtf32.Add((uint)rune.Value);

                for (int i = 0; i < utf16Length; i++)
                    allScalarsAsUtf16.Add(utf16[i]);

                for (int i = 0; i < utf8Length; i++)
                    allScalarsAsUtf8.Add(utf8[i]);
            }

            s_allScalarsAsUtf32 = allScalarsAsUtf32.ToArray().AsMemory();
            s_allScalarsAsUtf16 = allScalarsAsUtf16.ToArray().AsMemory();
            s_allScalarsAsUtf8 = allScalarsAsUtf8.ToArray().AsMemory();
        }

        /*
         * COMMON UTILITIES FOR UNIT TESTS
         */

        public static byte[] DecodeHex(ReadOnlySpan<char> inputHex)
        {
            Assert.True(Regex.IsMatch(inputHex.ToString(), "^([0-9a-fA-F]{2})*$"), "Input must be an even number of hex characters.");

            return Convert.FromHexString(inputHex);
        }

        public static byte[] ToUtf8(Rune rune)
        {
            Span<byte> utf8 = stackalloc byte[4];

            int length = ToUtf8(rune, utf8);
            return utf8.Slice(0, length).ToArray();
        }

        private static char[] ToUtf16(Rune rune)
        {
            Span<char> utf16 = stackalloc char[2];

            int length = ToUtf16(rune, utf16);
            return utf16.Slice(0, length).ToArray();
        }

        // !! IMPORTANT !!
        // Don't delete this implementation, as we use it as a reference to make sure the framework's
        // transcoding logic is correct.
        public static int ToUtf8(Rune rune, Span<byte> destination)
        {
            if (!Rune.IsValid(rune.Value))
            {
                Assert.True(Rune.IsValid(rune.Value), $"Rune with value U+{(uint)rune.Value:X4} is not well-formed.");
            }

            Assert.True(destination.Length == 4);

            destination[0] = 0;
            destination[1] = 0;
            destination[2] = 0;
            destination[3] = 0;

            if (rune.Value < 0x80)
            {
                destination[0] = (byte)rune.Value;
                return 1;
            }
            else if (rune.Value < 0x0800)
            {
                destination[0] = (byte)((rune.Value >> 6) | 0xC0);
                destination[1] = (byte)((rune.Value & 0x3F) | 0x80);
                return 2;
            }
            else if (rune.Value < 0x10000)
            {
                destination[0] = (byte)((rune.Value >> 12) | 0xE0);
                destination[1] = (byte)(((rune.Value >> 6) & 0x3F) | 0x80);
                destination[2] = (byte)((rune.Value & 0x3F) | 0x80);
                return 3;
            }
            else
            {
                destination[0] = (byte)((rune.Value >> 18) | 0xF0);
                destination[1] = (byte)(((rune.Value >> 12) & 0x3F) | 0x80);
                destination[2] = (byte)(((rune.Value >> 6) & 0x3F) | 0x80);
                destination[3] = (byte)((rune.Value & 0x3F) | 0x80);
                return 4;
            }
        }

        // !! IMPORTANT !!
        // Don't delete this implementation, as we use it as a reference to make sure the framework's
        // transcoding logic is correct.
        private static int ToUtf16(Rune rune, Span<char> destination)
        {
            if (!Rune.IsValid(rune.Value))
            {
                Assert.True(Rune.IsValid(rune.Value), $"Rune with value U+{(uint)rune.Value:X4} is not well-formed.");
            }

            Assert.True(destination.Length == 2);

            destination[0] = '\0';
            destination[1] = '\0';

            if (rune.IsBmp)
            {
                destination[0] = (char)rune.Value;
                return 1;
            }
            else
            {
                destination[0] = (char)((rune.Value >> 10) + 0xD800 - 0x40);
                destination[1] = (char)((rune.Value & 0x03FF) + 0xDC00);
                return 2;
            }
        }

        [Theory]
        [InlineData("", "")] // empty string is OK
        [InlineData(X_UTF16, X_UTF8)]
        [InlineData(E_ACUTE_UTF16, E_ACUTE_UTF8)]
        [InlineData(EURO_SYMBOL_UTF16, EURO_SYMBOL_UTF8)]
        public void Utf16ToUtf8_WithSmallValidBuffers(string utf16Input, string expectedUtf8TranscodingHex)
        {
            Assert.InRange(utf16Input.Length, 0, 1);

            Utf16ToUtf8_String_Test_Core(
                utf16Input: utf16Input,
                destinationSize: expectedUtf8TranscodingHex.Length / 2,
                expectedEncodingResult: CharacterEncodingResult.Success,
                expectedUtf8Transcoding: DecodeHex(expectedUtf8TranscodingHex));
        }

        [Theory]
        [InlineData('\uD800', CharacterEncodingResult.InsufficientLength)] // standalone high surrogate
        [InlineData('\uDFFF', CharacterEncodingResult.InvalidFormat)] // standalone low surrogate
        public void Utf16ToUtf8_WithOnlyStandaloneSurrogates(char charValue, CharacterEncodingResult expectedEncodingResult)
        {
            Utf16ToUtf8_String_Test_Core(
                utf16Input: new[] { charValue },
                destinationSize: 0,
                expectedEncodingResult: expectedEncodingResult,
                expectedUtf8Transcoding: Span<byte>.Empty);
        }

        [Theory]
        [InlineData("<LOW><HIGH>", "")] // swapped surrogate pair characters
        [InlineData("A<LOW><HIGH>", "41")] // consume standalone ASCII char, then swapped surrogate pair characters
        [InlineData("A<HIGH>B", "41F0")] // consume standalone ASCII char, then standalone high surrogate char
        [InlineData("A<LOW>B", "41")] // consume standalone ASCII char, then standalone low surrogate char
        [InlineData("AB<HIGH><HIGH>", "4142F0")] // consume two ASCII chars, then standalone high surrogate char
        [InlineData("AB<LOW><LOW>", "4142")] // consume two ASCII chars, then standalone low surrogate char
        public void Utf16ToUtf8_WithInvalidSurrogates(string utf16Input, string expectedUtf8TranscodingHex)
        {
            // xUnit can't handle ill-formed strings in [InlineData], so we replace here.

            utf16Input = utf16Input.Replace("<HIGH>", "\uD800").Replace("<LOW>", "\uDFFF");

            // These test cases are for the "fast processing" code which is the main loop of TranscodeToUtf8,
            // so inputs should be at least 2 chars.

            Assert.True(utf16Input.Length >= 2);

            Utf16ToUtf8_String_Test_Core(
                utf16Input: utf16Input,
                destinationSize: expectedUtf8TranscodingHex.Length / 2,
                expectedEncodingResult: CharacterEncodingResult.InvalidFormat,
                expectedUtf8Transcoding: DecodeHex(expectedUtf8TranscodingHex));

            // Now try the tests again with a larger buffer.
            // This ensures that running out of destination space wasn't the reason we failed.

            Utf16ToUtf8_String_Test_Core(
                utf16Input: utf16Input,
                destinationSize: (expectedUtf8TranscodingHex.Length) / 2 + 16,
                expectedEncodingResult: CharacterEncodingResult.InvalidFormat,
                expectedUtf8Transcoding: DecodeHex(expectedUtf8TranscodingHex));
        }

        [Theory]
        [InlineData("80", CharacterEncodingResult.InsufficientLength, "")] // sequence cannot begin with continuation character
        [InlineData("8182", CharacterEncodingResult.InsufficientLength, "")] // sequence cannot begin with continuation character
        [InlineData("838485", CharacterEncodingResult.InsufficientLength, "")] // sequence cannot begin with continuation character
        [InlineData(X_UTF8 + "80", CharacterEncodingResult.InsufficientLength, X_UTF16)] // sequence cannot begin with continuation character
        [InlineData(X_UTF8 + "8182", CharacterEncodingResult.InsufficientLength, X_UTF16)] // sequence cannot begin with continuation character
        [InlineData("C0", CharacterEncodingResult.InvalidFormat, "")] // [ C0 ] is always invalid
        [InlineData("C080", CharacterEncodingResult.InsufficientLength, "")] // [ C0 ] is always invalid
        [InlineData("C08081", CharacterEncodingResult.InsufficientLength, "")] // [ C0 ] is always invalid
        [InlineData(X_UTF8 + "C1", CharacterEncodingResult.InvalidFormat, X_UTF16)] // [ C1 ] is always invalid
        [InlineData(X_UTF8 + "C180", CharacterEncodingResult.InsufficientLength, X_UTF16)] // [ C1 ] is always invalid
        [InlineData(X_UTF8 + "C27F", CharacterEncodingResult.InsufficientLength, X_UTF16)] // [ C2 ] is improperly terminated
        [InlineData("E2827F", CharacterEncodingResult.InsufficientLength, "")] // [ E2 82 ] is improperly terminated
        [InlineData("E09F80", CharacterEncodingResult.InsufficientLength, "")] // [ E0 9F ... ] is overlong
        [InlineData("E0C080", CharacterEncodingResult.InsufficientLength, "")] // [ E0 ] is improperly terminated
        [InlineData("ED7F80", CharacterEncodingResult.InsufficientLength, "")] // [ ED ] is improperly terminated
        [InlineData("EDA080", CharacterEncodingResult.InsufficientLength, "")] // [ ED A0 ... ] is surrogate
        public void Utf8ToUtf16_WithSmallInvalidBuffers(string utf8HexInput, CharacterEncodingResult expectedEncodingResult, string expectedUtf16Transcoding)
        {
            Utf8ToUtf16_String_Test_Core(
              utf8Input: DecodeHex(utf8HexInput),
              destinationSize: expectedUtf16Transcoding.Length,
              expectedEncodingResult: expectedEncodingResult,
              expectedUtf16Transcoding: expectedUtf16Transcoding);

            // Now try the tests again with a larger buffer.
            // This ensures that the sequence is seen as invalid when not hitting the destination length check.

            Utf8ToUtf16_String_Test_Core(
                utf8Input: DecodeHex(utf8HexInput),
                destinationSize: expectedUtf16Transcoding.Length + 16,
                expectedEncodingResult: CharacterEncodingResult.InvalidFormat,
                expectedUtf16Transcoding: expectedUtf16Transcoding);
        }

        [Theory]
        /* SMALL VALID BUFFERS - tests drain loop at end of method */
        [InlineData("")] // empty string is OK
        [InlineData("X")]
        [InlineData("XY")]
        [InlineData("XYZ")]
        [InlineData(E_ACUTE_UTF16)]
        [InlineData(X_UTF16 + E_ACUTE_UTF16)]
        [InlineData(E_ACUTE_UTF16 + X_UTF16)]
        [InlineData(EURO_SYMBOL_UTF16)]
        /* LARGE VALID BUFFERS - test main loop at beginning of method */
        [InlineData(E_ACUTE_UTF16 + "ABCD" + "0123456789:;<=>?")] // Loop unrolling at end of buffer
        [InlineData(E_ACUTE_UTF16 + "ABCD" + "0123456789:;<=>?" + "01234567" + E_ACUTE_UTF16 + "89:;<=>?")] // Loop unrolling interrupted by non-ASCII
        [InlineData("ABC" + E_ACUTE_UTF16 + "0123")] // 3 ASCII bytes followed by non-ASCII
        [InlineData("AB" + E_ACUTE_UTF16 + "0123")] // 2 ASCII bytes followed by non-ASCII
        [InlineData("A" + E_ACUTE_UTF16 + "0123")] // 1 ASCII byte followed by non-ASCII
        [InlineData(E_ACUTE_UTF16 + E_ACUTE_UTF16 + E_ACUTE_UTF16 + E_ACUTE_UTF16)] // 4x 2-byte sequences, exercises optimization code path in 2-byte sequence processing
        [InlineData(E_ACUTE_UTF16 + E_ACUTE_UTF16 + E_ACUTE_UTF16 + "PQ")] // 3x 2-byte sequences + 2 ASCII bytes, exercises optimization code path in 2-byte sequence processing
        [InlineData(E_ACUTE_UTF16 + "PQ")] // single 2-byte sequence + 2 trailing ASCII bytes, exercises draining logic in 2-byte sequence processing
        [InlineData(E_ACUTE_UTF16 + "P" + E_ACUTE_UTF16 + "0@P")] // single 2-byte sequences + 1 trailing ASCII byte + 2-byte sequence, exercises draining logic in 2-byte sequence processing
        [InlineData(EURO_SYMBOL_UTF16 + "@")] // single 3-byte sequence + 1 trailing ASCII byte, exercises draining logic in 3-byte sequence processing
        [InlineData(EURO_SYMBOL_UTF16 + "@P`")] // single 3-byte sequence + 3 trailing ASCII byte, exercises draining logic and "running out of data" logic in 3-byte sequence processing
        [InlineData(EURO_SYMBOL_UTF16 + EURO_SYMBOL_UTF16 + EURO_SYMBOL_UTF16)] // 3x 3-byte sequences, exercises "stay within 3-byte loop" logic in 3-byte sequence processing
        [InlineData(EURO_SYMBOL_UTF16 + EURO_SYMBOL_UTF16 + EURO_SYMBOL_UTF16 + EURO_SYMBOL_UTF16)] // 4x 3-byte sequences, exercises "consume multiple bytes at a time" logic in 3-byte sequence processing
        [InlineData(EURO_SYMBOL_UTF16 + EURO_SYMBOL_UTF16 + EURO_SYMBOL_UTF16 + E_ACUTE_UTF16)] // 3x 3-byte sequences + single 2-byte sequence, exercises "consume multiple bytes at a time" logic in 3-byte sequence processing
        [InlineData(EURO_SYMBOL_UTF16 + EURO_SYMBOL_UTF16 + E_ACUTE_UTF16 + E_ACUTE_UTF16 + E_ACUTE_UTF16 + E_ACUTE_UTF16)] // 2x 3-byte sequences + 4x 2-byte sequences, exercises "consume multiple bytes at a time" logic in 3-byte sequence processing
        [InlineData(GRINNING_FACE_UTF16 + GRINNING_FACE_UTF16)] // 2x 4-byte sequences, exercises 4-byte sequence processing
        [InlineData(GRINNING_FACE_UTF16 + "@AB")] // single 4-byte sequence + 3 ASCII bytes, exercises 4-byte sequence processing and draining logic
        [InlineData(WOMAN_CARTWHEELING_MEDSKIN_UTF16)] // exercises switching between multiple sequence lengths
        public void Utf8ToUtf16_ValidBuffers(string utf16Input)
        {
            // We're going to run the tests with destination buffer lengths ranging from 0 all the way
            // to buffers large enough to hold the full output. This allows us to test logic that
            // detects whether we're about to overrun our destination buffer and instead returns DestinationTooSmall.

            Rune[] enumeratedScalars = utf16Input.EnumerateRunes().ToArray();

            // Convert entire input to UTF-8 using our unit test reference logic.

            byte[] utf8Input = enumeratedScalars.SelectMany(ToUtf8).ToArray();

            // 0-length buffer test
            Utf8ToUtf16_String_Test_Core(
                utf8Input: utf8Input,
                destinationSize: 0,
                expectedEncodingResult: (utf8Input.Length == 0) ? CharacterEncodingResult.Success : CharacterEncodingResult.InsufficientLength,
                expectedUtf16Transcoding: ReadOnlySpan<char>.Empty);

            char[] concatenatedUtf16 = Array.Empty<char>();

            for (int i = 0; i < enumeratedScalars.Length; i++)
            {
                Rune thisScalar = enumeratedScalars[i];

                // if this is an astral scalar value, quickly test a buffer that's not large enough to contain the entire UTF-16 encoding

                if (!thisScalar.IsBmp)
                {
                    Utf8ToUtf16_String_Test_Core(
                        utf8Input: utf8Input,
                        destinationSize: concatenatedUtf16.Length + 1,
                        expectedEncodingResult: CharacterEncodingResult.InsufficientLength,
                        expectedUtf16Transcoding: concatenatedUtf16);
                }

                // now provide a destination buffer large enough to hold the next full scalar encoding

                concatenatedUtf16 = concatenatedUtf16.Concat(ToUtf16(thisScalar)).ToArray();

                Utf8ToUtf16_String_Test_Core(
                    utf8Input: utf8Input,
                    destinationSize: concatenatedUtf16.Length,
                    expectedEncodingResult: (i == enumeratedScalars.Length - 1) ? CharacterEncodingResult.Success : CharacterEncodingResult.InsufficientLength,
                    expectedUtf16Transcoding: concatenatedUtf16);
            }

            // now throw lots of ASCII data at the beginning so that we exercise the vectorized code paths

            utf16Input = new string('x', 64) + utf16Input;
            utf8Input = utf16Input.EnumerateRunes().SelectMany(ToUtf8).ToArray();

            Utf8ToUtf16_String_Test_Core(
                utf8Input: utf8Input,
                destinationSize: utf16Input.Length,
                expectedEncodingResult: CharacterEncodingResult.Success,
                expectedUtf16Transcoding: utf16Input);

            // now throw some non-ASCII data at the beginning so that we *don't* exercise the vectorized code paths

            utf16Input = WOMAN_CARTWHEELING_MEDSKIN_UTF16 + utf16Input[64..];
            utf8Input = utf16Input.EnumerateRunes().SelectMany(ToUtf8).ToArray();

            Utf8ToUtf16_String_Test_Core(
                utf8Input: utf8Input,
                destinationSize: utf16Input.Length,
                expectedEncodingResult: CharacterEncodingResult.Success,
                expectedUtf16Transcoding: utf16Input);
        }

        [Fact]
        public void Utf8ToUtf16_String_AllPossibleScalarValues()
        {
            Utf8ToUtf16_String_Test_Core(
                utf8Input: s_allScalarsAsUtf8.Span,
                destinationSize: s_allScalarsAsUtf16.Length,
                expectedEncodingResult: CharacterEncodingResult.Success,
                expectedUtf16Transcoding: s_allScalarsAsUtf16.Span);
        }

        [Fact]
        public void Utf16ToUtf8_String_AllPossibleScalarValues()
        {
            Utf16ToUtf8_String_Test_Core(
                utf16Input: s_allScalarsAsUtf16.Span,
                destinationSize: s_allScalarsAsUtf8.Length,
                expectedEncodingResult: CharacterEncodingResult.Success,
                expectedUtf8Transcoding: s_allScalarsAsUtf8.Span);
        }

        [Fact]
        public void Utf8ToUtf32_String_AllPossibleScalarValues()
        {
            Utf8ToUtf32_String_Test_Core(
                utf8Input: s_allScalarsAsUtf8.Span,
                destinationSize: s_allScalarsAsUtf32.Length,
                expectedEncodingResult: CharacterEncodingResult.Success,
                expectedUtf32Transcoding: s_allScalarsAsUtf32.Span);
        }

        [Fact]
        public void Utf32ToUtf8_String_AllPossibleScalarValues()
        {
            Utf32ToUtf8_String_Test_Core(
                utf32Input: s_allScalarsAsUtf32.Span,
                destinationSize: s_allScalarsAsUtf8.Length,
                expectedEncodingResult: CharacterEncodingResult.Success,
                expectedUtf8Transcoding: s_allScalarsAsUtf8.Span);
        }

        [Fact]
        public void Utf8ToUtf16_Length_AllPossibleScalarValues()
        {
            Utf8ToUtf16_Length_Test_Core(
                // Skip the first code point because it's 0
                utf8Input: s_allScalarsAsUtf8.Span.Slice(1),
                expectedEncodingResult: CharacterEncodingResult.Success,
                expectedUtf16Length: s_allScalarsAsUtf16.Length - 1);
        }

        [Fact]
        public void Utf16ToUtf8_Length_AllPossibleScalarValues()
        {
            Utf16ToUtf8_Length_Test_Core(
                utf16Input: s_allScalarsAsUtf16.Span.Slice(1),
                expectedEncodingResult: CharacterEncodingResult.Success,
                expectedUtf8Length: s_allScalarsAsUtf8.Length - 1);
        }

        [Fact]
        public void Utf8ToUtf32_Length_AllPossibleScalarValues()
        {
            Utf8ToUtf32_Length_Test_Core(
                utf8Input: s_allScalarsAsUtf8.Span.Slice(1),
                expectedEncodingResult: CharacterEncodingResult.Success,
                expectedUtf32Length: s_allScalarsAsUtf32.Length - 1);
        }

        [Fact]
        public void Utf32ToUtf8_Length_AllPossibleScalarValues()
        {
            Utf32ToUtf8_Length_Test_Core(
                utf32Input: s_allScalarsAsUtf32.Span.Slice(1),
                expectedEncodingResult: CharacterEncodingResult.Success,
                expectedUtf8Length: s_allScalarsAsUtf8.Length - 1);
        }

        [Fact]
        public void Utf8ToUtf16_Character_AllPossibleScalarValues()
        {
            Span<byte> utf8 = stackalloc byte[4];
            Span<char> utf16 = stackalloc char[2];

            foreach (Rune rune in s_allValidScalars)
            {
                ToUtf8(rune, utf8);
                ToUtf16(rune, utf16);

                Utf8ToUtf16_Character_Test_Core(
                    utf8Input: utf8,
                    expectedEncodingResult: CharacterEncodingResult.Success,
                    expectedUtf16Transcoding: utf16);
            }
        }

        [Fact]
        public void Utf16ToUtf8_Character_AllPossibleScalarValues()
        {
            Span<byte> utf8 = stackalloc byte[4];
            Span<char> utf16 = stackalloc char[2];

            foreach (Rune rune in s_allValidScalars)
            {
                ToUtf8(rune, utf8);
                ToUtf16(rune, utf16);

                Utf16ToUtf8_Character_Test_Core(
                    utf16Input: utf16,
                    expectedEncodingResult: CharacterEncodingResult.Success,
                    expectedUtf8Transcoding: utf8);
            }
        }

        [Fact]
        public void Utf8ToUtf32_Character_AllPossibleScalarValues()
        {
            Span<byte> utf8 = stackalloc byte[4];

            foreach (Rune rune in s_allValidScalars)
            {
                ToUtf8(rune, utf8);

                Utf8ToUtf32_Character_Test_Core(
                    utf8Input: utf8,
                    expectedEncodingResult: CharacterEncodingResult.Success,
                    expectedUtf32Transcoding: (uint)rune.Value);
            }
        }

        [Fact]
        public void Utf32ToUtf8_Character_AllPossibleScalarValues()
        {
            Span<byte> utf8 = stackalloc byte[4];

            foreach (Rune rune in s_allValidScalars)
            {
                ToUtf8(rune, utf8);

                Utf32ToUtf8_Character_Test_Core(
                    utf32Input: (uint)rune.Value,
                    expectedEncodingResult: CharacterEncodingResult.Success,
                    expectedUtf8Transcoding: utf8);
            }
        }

        [Fact]
        public void PickOutCharacterFromUtf8String_AllPossibleScalarValues()
        {
            byte[] expectedUtf8 = new byte[4];
            byte[] actualUtf8 = new byte[4];
            
            ReadOnlySpan<byte> utf8Values = s_allScalarsAsUtf8.Span.Slice(1);

            foreach (Rune rune in s_allValidScalars.Skip(1))
            {
                ToUtf8(rune, expectedUtf8);

                CharacterEncodingResult result = CharacterEncoding.PickOutCharacterFromUtf8String(actualUtf8, ref utf8Values);

                Assert.Equal(CharacterEncodingResult.Success, result);
                Assert.Equal((ReadOnlySpan<byte>)expectedUtf8, actualUtf8);
            }
        }

        private static void Utf8ToUtf16_String_Test_Core(ReadOnlySpan<byte> utf8Input, int destinationSize, CharacterEncodingResult expectedEncodingResult, ReadOnlySpan<char> expectedUtf16Transcoding)
        {
            char[] destination = new char[destinationSize];

            CharacterEncodingResult actualEncodingResult = CharacterEncoding.ConvertStringUtf8ToUtf16Native(destination, utf8Input, utf8Input.Length);

            Assert.Equal(expectedEncodingResult, actualEncodingResult);
            Assert.Equal(expectedUtf16Transcoding, destination.AsSpan(0, expectedUtf16Transcoding.Length));
        }

        private static void Utf16ToUtf8_String_Test_Core(ReadOnlySpan<char> utf16Input, int destinationSize, CharacterEncodingResult expectedEncodingResult, ReadOnlySpan<byte> expectedUtf8Transcoding)
        {
            byte[] destination = new byte[destinationSize];

            CharacterEncodingResult actualEncodingResult = CharacterEncoding.ConvertStringUtf16NativeToUtf8(destination, utf16Input, utf16Input.Length);

            Assert.Equal(expectedEncodingResult, actualEncodingResult);
            Assert.Equal(expectedUtf8Transcoding, destination.AsSpan(0, expectedUtf8Transcoding.Length));
        }

        private static void Utf8ToUtf32_String_Test_Core(ReadOnlySpan<byte> utf8Input, int destinationSize, CharacterEncodingResult expectedEncodingResult, ReadOnlySpan<uint> expectedUtf32Transcoding)
        {
            uint[] destination = new uint[destinationSize];

            CharacterEncodingResult actualEncodingResult = CharacterEncoding.ConvertStringUtf8ToUtf32(destination, utf8Input, utf8Input.Length);

            Assert.Equal(expectedEncodingResult, actualEncodingResult);
            Assert.Equal(expectedUtf32Transcoding, destination.AsSpan(0, expectedUtf32Transcoding.Length));
        }

        private static void Utf32ToUtf8_String_Test_Core(ReadOnlySpan<uint> utf32Input, int destinationSize, CharacterEncodingResult expectedEncodingResult, ReadOnlySpan<byte> expectedUtf8Transcoding)
        {
            byte[] destination = new byte[destinationSize];

            CharacterEncodingResult actualEncodingResult = CharacterEncoding.ConvertStringUtf32ToUtf8(destination, utf32Input, utf32Input.Length);

            Assert.Equal(expectedEncodingResult, actualEncodingResult);
            Assert.Equal(expectedUtf8Transcoding, destination.AsSpan(0, expectedUtf8Transcoding.Length));
        }

        private static void Utf8ToUtf16_Length_Test_Core(ReadOnlySpan<byte> utf8Input, CharacterEncodingResult expectedEncodingResult, int expectedUtf16Length)
        {
            CharacterEncodingResult actualEncodingResult = CharacterEncoding.GetLengthOfConvertedStringUtf8ToUtf16Native(out int actualLength, utf8Input);

            Assert.Equal(expectedEncodingResult, actualEncodingResult);
            Assert.Equal(expectedUtf16Length, actualLength);
        }

        private static void Utf16ToUtf8_Length_Test_Core(ReadOnlySpan<char> utf16Input, CharacterEncodingResult expectedEncodingResult, int expectedUtf8Length)
        {
            CharacterEncodingResult actualEncodingResult = CharacterEncoding.GetLengthOfConvertedStringUtf16NativeToUtf8(out int actualLength, utf16Input);

            Assert.Equal(expectedEncodingResult, actualEncodingResult);
            Assert.Equal(expectedUtf8Length, actualLength);
        }

        private static void Utf8ToUtf32_Length_Test_Core(ReadOnlySpan<byte> utf8Input, CharacterEncodingResult expectedEncodingResult, int expectedUtf32Length)
        {
            CharacterEncodingResult actualEncodingResult = CharacterEncoding.GetLengthOfConvertedStringUtf8ToUtf32(out int actualLength, utf8Input);

            Assert.Equal(expectedEncodingResult, actualEncodingResult);
            Assert.Equal(expectedUtf32Length, actualLength);
        }

        private static void Utf32ToUtf8_Length_Test_Core(ReadOnlySpan<uint> utf32Input, CharacterEncodingResult expectedEncodingResult, int expectedUtf8Length)
        {
            CharacterEncodingResult actualEncodingResult = CharacterEncoding.GetLengthOfConvertedStringUtf32ToUtf8(out int actualLength, utf32Input);

            Assert.Equal(expectedEncodingResult, actualEncodingResult);
            Assert.Equal(expectedUtf8Length, actualLength);
        }

        private static void Utf8ToUtf16_Character_Test_Core(ReadOnlySpan<byte> utf8Input, CharacterEncodingResult expectedEncodingResult, ReadOnlySpan<char> expectedUtf16Transcoding)
        {
            Span<char> destination = stackalloc char[2];

            CharacterEncodingResult actualEncodingResult = CharacterEncoding.ConvertCharacterUtf8ToUtf16Native(destination, utf8Input);

            Assert.Equal(expectedEncodingResult, actualEncodingResult);
            Assert.Equal(expectedUtf16Transcoding, destination.Slice(0, expectedUtf16Transcoding.Length));

            for (int i = expectedUtf16Transcoding.Length; i < destination.Length; i++)
            {
                Assert.Equal(0, destination[i]);
            }
        }

        private static void Utf16ToUtf8_Character_Test_Core(ReadOnlySpan<char> utf16Input, CharacterEncodingResult expectedEncodingResult, ReadOnlySpan<byte> expectedUtf8Transcoding)
        {
            Span<byte> destination = stackalloc byte[4];

            CharacterEncodingResult actualEncodingResult = CharacterEncoding.ConvertCharacterUtf16NativeToUtf8(destination, utf16Input);

            Assert.Equal(expectedEncodingResult, actualEncodingResult);
            Assert.Equal(expectedUtf8Transcoding, destination.Slice(0, expectedUtf8Transcoding.Length));

            for (int i = expectedUtf8Transcoding.Length; i < destination.Length; i++)
            {
                Assert.Equal(0, destination[i]);
            }
        }

        private static void Utf8ToUtf32_Character_Test_Core(ReadOnlySpan<byte> utf8Input, CharacterEncodingResult expectedEncodingResult, uint expectedUtf32Transcoding)
        {
            CharacterEncodingResult actualEncodingResult = CharacterEncoding.ConvertCharacterUtf8ToUtf32(out uint destination, utf8Input);

            Assert.Equal(expectedEncodingResult, actualEncodingResult);
            Assert.Equal(expectedUtf32Transcoding, destination);
        }

        private static void Utf32ToUtf8_Character_Test_Core(uint utf32Input, CharacterEncodingResult expectedEncodingResult, ReadOnlySpan<byte> expectedUtf8Transcoding)
        {
            Span<byte> destination = stackalloc byte[4];

            CharacterEncodingResult actualEncodingResult = CharacterEncoding.ConvertCharacterUtf32ToUtf8(destination, utf32Input);

            Assert.Equal(expectedEncodingResult, actualEncodingResult);
            Assert.Equal(expectedUtf8Transcoding, destination.Slice(0, expectedUtf8Transcoding.Length));

            for (int i = expectedUtf8Transcoding.Length; i < destination.Length; i++)
            {
                Assert.Equal(0, destination[i]);
            }
        }
    }
}
