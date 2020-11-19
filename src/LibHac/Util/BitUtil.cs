using System.Runtime.CompilerServices;

namespace LibHac.Util
{
    public static class BitUtil
    {
        public static bool IsPowerOfTwo(int value)
        {
            return value > 0 && ResetLeastSignificantOneBit(value) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOfTwo(long value)
        {
            return value > 0 && ResetLeastSignificantOneBit(value) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOfTwo(ulong value)
        {
            return value > 0 && ResetLeastSignificantOneBit(value) == 0;
        }

        private static int ResetLeastSignificantOneBit(int value)
        {
            return value & (value - 1);
        }

        private static long ResetLeastSignificantOneBit(long value)
        {
            return value & (value - 1);
        }

        private static ulong ResetLeastSignificantOneBit(ulong value)
        {
            return value & (value - 1);
        }

        // DivideUp comes from a C++ template that always casts to unsigned types
        public static uint DivideUp(uint value, uint divisor) => (value + divisor - 1) / divisor;
        public static ulong DivideUp(ulong value, ulong divisor) => (value + divisor - 1) / divisor;

        public static int DivideUp(int value, int divisor) => (int)DivideUp((uint)value, (uint)divisor);
        public static long DivideUp(long value, long divisor) => (long)DivideUp((ulong)value, (ulong)divisor);
    }
}
