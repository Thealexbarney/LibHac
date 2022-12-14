using System.Numerics;
using System.Runtime.CompilerServices;

namespace LibHac.Util;

public static class BitUtil
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPowerOfTwo<T>(T value) where T : IBitwiseOperators<T, T, T>, INumberBase<T>
    {
        return !T.IsNegative(value) && !T.IsZero(value) && T.IsZero(ResetLeastSignificantOneBit(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ResetLeastSignificantOneBit<T>(T value) where T : IBitwiseOperators<T, T, T>, INumberBase<T>
    {
        return value & unchecked(value - T.One);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountLeadingZeros<T>(T value) where T : IBinaryInteger<T>
    {
        return int.CreateTruncating(T.LeadingZeroCount(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T DivideUp<T>(T value, T divisor) where T : INumberBase<T>
    {
        return unchecked(value + divisor - T.One) / divisor;
    }
}