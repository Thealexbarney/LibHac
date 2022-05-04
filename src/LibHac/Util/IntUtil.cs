namespace LibHac.Util;

public static class IntUtil
{
    // Todo: Use generic math once C# 11 is out
    public static bool IsIntValueRepresentableAsLong(ulong value)
    {
        return value <= long.MaxValue;
    }

    public static bool IsIntValueRepresentableAsULong(long value)
    {
        return value >= 0;
    }

    public static bool IsIntValueRepresentableAsInt(long value)
    {
        return value >= int.MinValue && value <= int.MaxValue;
    }

    public static bool IsIntValueRepresentableAsUInt(long value)
    {
        return value >= uint.MinValue && value <= uint.MaxValue;
    }

    public static bool CanAddWithoutOverflow(long x, long y)
    {
        if (y >= 0)
        {
            return x <= long.MaxValue - y;
        }
        else
        {
            return x >= unchecked(long.MinValue - y);
        }
    }

    public static bool CanAddWithoutOverflow(ulong x, ulong y)
    {
        return x <= ulong.MaxValue - y;
    }
}