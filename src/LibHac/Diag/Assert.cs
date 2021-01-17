using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace LibHac.Diag
{
    public static class Assert
    {
        [Conditional("DEBUG")]
        public static void True([DoesNotReturnIf(false)] bool condition, string message = null)
        {
            if (condition)
                return;

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new LibHacException("Assertion failed.");
            }

            throw new LibHacException($"Assertion failed: {message}");
        }

        [Conditional("DEBUG")]
        public static void False([DoesNotReturnIf(true)] bool condition, string message = null)
        {
            if (!condition)
                return;

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new LibHacException("Assertion failed.");
            }

            throw new LibHacException($"Assertion failed: {message}");
        }

        [Conditional("DEBUG")]
        public static void Null<T>([NotNull] T item) where T : class
        {
            if (!(item is null))
            {
                throw new LibHacException("Null assertion failed.");
            }
        }

        [Conditional("DEBUG")]
        public static void NotNull<T>([NotNull] T item) where T : class
        {
            if (item is null)
            {
                throw new LibHacException("Not-null assertion failed.");
            }
        }

        [Conditional("DEBUG")]
        public static void InRange(int value, int lowerInclusive, int upperExclusive)
        {
            InRange((long)value, lowerInclusive, upperExclusive);
        }

        [Conditional("DEBUG")]
        public static void InRange(long value, long lowerInclusive, long upperExclusive)
        {
            if (value < lowerInclusive || value >= upperExclusive)
            {
                throw new LibHacException($"Value {value} is not in the range {lowerInclusive} to {upperExclusive}");
            }
        }

        public static void Equal<T>(T value1, T value2) where T : IEquatable<T>
        {
            if (!value1.Equals(value2))
            {
                throw new LibHacException($"Values were not equal: {value1}, {value2}");
            }
        }

        public static void NotEqual<T>(T value1, T value2) where T : IEquatable<T>
        {
            if (value1.Equals(value2))
            {
                throw new LibHacException($"Values should not be equal: {value1}, {value2}");
            }
        }
    }
}
