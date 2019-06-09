namespace LibHac
{
    internal static class ThrowHelper
    {
        public static void ThrowResult(Result result) => throw new HorizonResultException(result);
        public static void ThrowResult(Result result, string message) => throw new HorizonResultException(result, message);
    }
}
