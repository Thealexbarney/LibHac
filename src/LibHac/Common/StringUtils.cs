using System;
using System.Text;

namespace LibHac.Common
{
    public static class StringUtils
    {
        public static int Copy(Span<byte> dest, ReadOnlySpan<byte> source)
        {
            int maxLen = Math.Min(dest.Length, source.Length);

            int i;
            for (i = 0; i < maxLen && source[i] != 0; i++)
                dest[i] = source[i];

            if (i < dest.Length)
            {
                dest[i] = 0;
            }

            return i;
        }

        public static int GetLength(ReadOnlySpan<byte> s)
        {
            int i = 0;

            while (i < s.Length && s[i] != 0)
            {
                i++;
            }

            return i;
        }

        public static int GetLength(ReadOnlySpan<byte> s, int maxLen)
        {
            int i = 0;

            while (i < maxLen && i < s.Length && s[i] != 0)
            {
                i++;
            }

            return i;
        }

        public static int Compare(ReadOnlySpan<byte> s1, ReadOnlySpan<byte> s2)
        {
            int i = 0;

            while (true)
            {
                int c1 = ((uint)i < (uint)s1.Length ? s1[i] : 0);
                int c2 = ((uint)i < (uint)s2.Length ? s2[i] : 0);

                if (c1 != c2)
                    return c1 - c2;

                if (c1 == 0)
                    return 0;

                i++;
            }
        }

        public static int Compare(ReadOnlySpan<byte> s1, ReadOnlySpan<byte> s2, int maxLen)
        {
            for (int i = 0; i < maxLen; i++)
            {
                int c1 = ((uint)i < (uint)s1.Length ? s1[i] : 0);
                int c2 = ((uint)i < (uint)s2.Length ? s2[i] : 0);

                if (c1 != c2)
                    return c1 - c2;

                if (c1 == 0)
                    return 0;
            }

            return 0;
        }

        public static int CompareCaseInsensitive(ReadOnlySpan<byte> s1, ReadOnlySpan<byte> s2)
        {
            int i = 0;

            while (true)
            {
                int c1 = ((uint)i < (uint)s1.Length ? ToLowerAsciiInvariant(s1[i]) : 0);
                int c2 = ((uint)i < (uint)s2.Length ? ToLowerAsciiInvariant(s2[i]) : 0);

                if (c1 != c2)
                    return c1 - c2;

                if (c1 == 0)
                    return 0;

                i++;
            }
        }

        public static int CompareCaseInsensitive(ReadOnlySpan<byte> s1, ReadOnlySpan<byte> s2, int maxLen)
        {
            for (int i = 0; i < maxLen; i++)
            {
                int c1 = ((uint)i < (uint)s1.Length ? ToLowerAsciiInvariant(s1[i]) : 0);
                int c2 = ((uint)i < (uint)s2.Length ? ToLowerAsciiInvariant(s2[i]) : 0);

                if (c1 != c2)
                    return c1 - c2;

                if (c1 == 0)
                    return 0;
            }

            return 0;
        }

        private static byte ToLowerAsciiInvariant(byte c)
        {
            if ((uint)(c - 'A') <= 'Z' - 'A')
            {
                c = (byte)(c | 0x20);
            }
            return c;
        }

        /// <summary>
        /// Concatenates 2 byte strings.
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="source"></param>
        /// <returns>The length of the resulting string.</returns>
        /// <remarks>This function appends the source string to the end of the null-terminated destination string.
        /// If the destination buffer is not large enough to contain the resulting string,
        /// bytes from the source string will be appended to the destination string util the buffer is full.
        /// If the length of the final string is the same length of the destination buffer,
        /// no null terminating byte will be written to the end of the string.</remarks>
        public static int Concat(Span<byte> dest, ReadOnlySpan<byte> source)
        {
            return Concat(dest, source, GetLength(dest));
        }

        public static int Concat(Span<byte> dest, ReadOnlySpan<byte> source, int destLength)
        {
            int iDest = destLength;

            for (int i = 0; iDest < dest.Length && i < source.Length && source[i] != 0; i++, iDest++)
            {
                dest[iDest] = source[i];
            }

            if (iDest < dest.Length)
            {
                dest[iDest] = 0;
            }

            return iDest;
        }

        public static string Utf8ToString(ReadOnlySpan<byte> value)
        {
            return Encoding.UTF8.GetString(value);
        }

        public static string Utf8ZToString(ReadOnlySpan<byte> value)
        {
            return Utf8ToString(value.Slice(0, GetLength(value)));
        }

        public static bool IsAlpha(byte c)
        {
            return (c | 0x20u) - (byte)'a' <= 'z' - 'a';
        }

        public static bool IsDigit(byte c)
        {
            return (uint)(c - (byte)'0') <= 9;
        }
    }
}
