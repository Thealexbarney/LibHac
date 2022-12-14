using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Diag;

namespace LibHac.Util;

public static class Alignment
{
    public static T AlignUp<T>(T value, ulong alignment) where T : IBinaryNumber<T>
    {
        Assert.SdkRequires(BitUtil.IsPowerOfTwo(alignment) || alignment == 0);

        unchecked
        {
            ulong invMask = alignment - 1;
            return T.CreateTruncating((ulong.CreateTruncating(value) + invMask) & ~invMask);
        }
    }

    public static T AlignDown<T>(T value, ulong alignment) where T : IBinaryNumber<T>
    {
        Assert.SdkRequires(BitUtil.IsPowerOfTwo(alignment) || alignment == 0);

        unchecked
        {
            ulong invMask = alignment - 1;
            return T.CreateTruncating(ulong.CreateTruncating(value) & ~invMask);
        }
    }

    public static T AlignDown<T>(T value, long alignment) where T : IBinaryNumber<T> => AlignDown(value, (ulong)alignment);

    public static bool IsAligned<T>(T value, ulong alignment) where T : IBinaryNumber<T>
    {
        Assert.SdkRequires(BitUtil.IsPowerOfTwo(alignment) || alignment == 0);

        unchecked
        {
            ulong invMask = alignment - 1;
            return (ulong.CreateTruncating(value) & invMask) == 0;
        }
    }

    public static bool IsAligned<T>(ReadOnlySpan<T> buffer, ulong alignment)
    {
        return IsAligned(ref MemoryMarshal.GetReference(buffer), alignment);
    }

    public static unsafe bool IsAligned<T>(ref T pointer, ulong alignment)
    {
        return IsAligned((ulong)Unsafe.AsPointer(ref pointer), alignment);
    }

    public static T GetAlignment<T>(T value) where T : IUnsignedNumber<T>, IBinaryInteger<T>
    {
        return unchecked(value & -value);
    }
}