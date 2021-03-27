using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace LibHac.Diag
{
    public ref struct AbortInfo
    {
        public AbortReason AbortReason;
        public string Message;
        public string Condition;
        public string FunctionName;
        public string FileName;
        public int LineNumber;
    }

    public enum AbortReason
    {
        SdkAssert,
        SdkRequires,
        UserAssert,
        Abort,
        UnexpectedDefault
    }

    public static class Abort
    {
        internal static void InvokeAbortObserver(in AbortInfo abortInfo)
        {
            // Todo
        }

        [DoesNotReturn]
        public static void DoAbort(Result result, string message = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new HorizonResultException(result, "Abort.");
            }

            throw new LibHacException($"Abort: {message}");
        }

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

            DoAbort(default, message);
        }

        public static void DoAbortUnless([DoesNotReturnIf(false)] bool condition, Result result, string message = null)
        {
            if (condition)
                return;

            result.Log();
            DoAbort(result, message);
        }

        [DoesNotReturn]
        public static void UnexpectedDefault([CallerMemberName] string caller = "")
        {
            throw new LibHacException($"Unexpected value passed to switch statement in {caller}");
        }
    }
}
