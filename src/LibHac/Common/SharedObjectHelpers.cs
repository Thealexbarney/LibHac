using System.Runtime.CompilerServices;

namespace LibHac.Common
{
    public static class Shared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Move<T>(ref T value)
        {
            T tmp = value;
            value = default;
            return tmp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Move<T>(out T dest, ref T value)
        {
            dest = value;
            value = default;
        }
    }
}
