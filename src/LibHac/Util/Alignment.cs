using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Diag;

namespace LibHac.Util
{
    public static class Alignment
    {
        // The alignment functions in this class come from C++ templates that always cast to unsigned types

        public static ulong AlignUpPow2(ulong value, uint alignment)
        {
            Assert.SdkRequires(BitUtil.IsPowerOfTwo(alignment));

            ulong invMask = alignment - 1;
            return ((value + invMask) & ~invMask);
        }

        public static ulong AlignDownPow2(ulong value, uint alignment)
        {
            Assert.SdkRequires(BitUtil.IsPowerOfTwo(alignment));

            ulong invMask = alignment - 1;
            return (value & ~invMask);
        }

        public static bool IsAlignedPow2(ulong value, uint alignment)
        {
            Assert.SdkRequires(BitUtil.IsPowerOfTwo(alignment));

            ulong invMask = alignment - 1;
            return (value & invMask) == 0;
        }

        public static bool IsAlignedPow2<T>(ReadOnlySpan<T> buffer, uint alignment)
        {
            return IsAlignedPow2(ref MemoryMarshal.GetReference(buffer), alignment);
        }

        public static unsafe bool IsAlignedPow2<T>(ref T pointer, uint alignment)
        {
            return IsAlignedPow2((ulong)Unsafe.AsPointer(ref pointer), alignment);
        }

        public static int AlignUpPow2(int value, uint alignment) => (int)AlignUpPow2((ulong)value, alignment);
        public static long AlignUpPow2(long value, uint alignment) => (long)AlignUpPow2((ulong)value, alignment);
        public static int AlignDownPow2(int value, uint alignment) => (int)AlignDownPow2((ulong)value, alignment);
        public static long AlignDownPow2(long value, uint alignment) => (long)AlignDownPow2((ulong)value, alignment);
        public static bool IsAlignedPow2(int value, uint alignment) => IsAlignedPow2((ulong)value, alignment);
        public static bool IsAlignedPow2(long value, uint alignment) => IsAlignedPow2((ulong)value, alignment);

        public static ulong AlignUp(ulong value, uint alignment) => AlignDown(value + alignment - 1, alignment);
        public static ulong AlignDown(ulong value, uint alignment) => value - value % alignment;
        public static bool IsAligned(ulong value, uint alignment) => value % alignment == 0;

        public static int AlignUp(int value, uint alignment) => (int)AlignUp((ulong)value, alignment);
        public static long AlignUp(long value, uint alignment) => (long)AlignUp((ulong)value, alignment);
        public static int AlignDown(int value, uint alignment) => (int)AlignDown((ulong)value, alignment);
        public static long AlignDown(long value, uint alignment) => (long)AlignDown((ulong)value, alignment);
        public static bool IsAligned(int value, uint alignment) => IsAligned((ulong)value, alignment);
        public static bool IsAligned(long value, uint alignment) => IsAligned((ulong)value, alignment);
    }
}
