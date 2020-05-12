using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace LibHac.Diag
{
    public static class Assert
    {
        [Conditional("DEBUG")]
        public static void AssertTrue([DoesNotReturnIf(false)] bool condition, string message = null)
        {
            if (condition)
                return;

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new LibHacException("Assertion failed.");
            }

            throw new LibHacException($"Assertion failed: {message}");
        }
    }
}
