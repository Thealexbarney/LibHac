using System;

namespace LibHac
{
    internal static class ThrowHelper
    {
        public static void ThrowResult(Result result) => throw new HorizonResultException(result);

        public static void ThrowResult(Result result, Exception innerException) =>
            throw new HorizonResultException(result, string.Empty, innerException);

        public static void ThrowResult(Result result, string message) =>
            throw new HorizonResultException(result, message);

        public static void ThrowResult(Result result, string message, Exception innerException) =>
            throw new HorizonResultException(result, message, innerException);

        internal static void ThrowArgumentOutOfRangeException()
        {
            throw new ArgumentOutOfRangeException();
        }
    }
}
