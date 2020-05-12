using System.Diagnostics.CodeAnalysis;

namespace LibHac.Diag
{
    public static class Abort
    {
        [DoesNotReturn]
        public static void DoAbort(string message = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new LibHacException("Abort.");
            }

            throw new LibHacException($"Abort: {message}");
        }

        public static void DoAbortUnless([DoesNotReturnIf(false)] bool condition, string message = null)
        {
            if (condition)
                return;

            DoAbort(message);
        }
    }
}
