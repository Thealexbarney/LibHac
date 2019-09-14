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
            return Concat(dest, GetLength(dest), source);
        }

        public static int Concat(Span<byte> dest, int destLength, ReadOnlySpan<byte> source)
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
        
        public static string FromUtf8Z(this Span<byte> value) => FromUtf8Z((ReadOnlySpan<byte>)value);

        public static string FromUtf8Z(this ReadOnlySpan<byte> value)
        {
            int i;
            for (i = 0; i < value.Length && value[i] != 0; i++) { }

            value = value.Slice(0, i);

#if STRING_SPAN
            return Encoding.UTF8.GetString(value);
#else
            return Encoding.UTF8.GetString(value.ToArray());
#endif
        }
    }
}
