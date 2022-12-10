using System.Numerics;
using System.Runtime.CompilerServices;

namespace LibHac.Util;

public static class IntUtil
{
    private static bool IsSignedType<T>() where T : INumber<T> => unchecked(T.IsNegative(T.Zero - T.One));

    // Todo .NET 8: Remove NoInlining. .NET 7's JIT doesn't inline everything properly
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool IsIntValueRepresentable<TTo, TFrom>(TFrom value)
        where TTo : IMinMaxValue<TTo>, INumber<TTo>
        where TFrom : IMinMaxValue<TFrom>, INumber<TFrom>
    {
        if (IsSignedType<TFrom>())
        {
            if (IsSignedType<TTo>())
            {
                return IsIntValueRepresentableImplSToS<TTo, TFrom>(value);
            }
            else
            {
                return IsIntValueRepresentableImplSToU<TTo, TFrom>(value);
            }
        }
        else
        {
            if (IsSignedType<TTo>())
            {
                return IsIntValueRepresentableImplUToS<TTo, TFrom>(value);
            }
            else
            {
                return IsIntValueRepresentableImplUToU<TTo, TFrom>(value);
            }
        }
    }

    // Methods for the 4 signed/unsigned TTo/TFrom permutations.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIntValueRepresentableImplSToS<TTo, TFrom>(TFrom value)
        where TTo : IMinMaxValue<TTo>, INumber<TTo>
        where TFrom : IMinMaxValue<TFrom>, INumber<TFrom>
    {
        if (long.CreateTruncating(TTo.MinValue) <= long.CreateTruncating(TFrom.MinValue) &&
            long.CreateTruncating(TFrom.MaxValue) <= long.CreateTruncating(TTo.MaxValue))
        {
            return true;
        }

        return long.CreateTruncating(TTo.MinValue) <= long.CreateTruncating(value) &&
               long.CreateTruncating(value) <= long.CreateTruncating(TTo.MaxValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIntValueRepresentableImplUToU<TTo, TFrom>(TFrom value)
        where TTo : IMinMaxValue<TTo>, INumber<TTo>
        where TFrom : IMinMaxValue<TFrom>, INumber<TFrom>
    {
        if (ulong.CreateTruncating(TTo.MinValue) <= ulong.CreateTruncating(TFrom.MinValue) &&
            ulong.CreateTruncating(TFrom.MaxValue) <= ulong.CreateTruncating(TTo.MaxValue))
        {
            return true;
        }

        return ulong.CreateTruncating(TTo.MinValue) <= ulong.CreateTruncating(value) &&
               ulong.CreateTruncating(value) <= ulong.CreateTruncating(TTo.MaxValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIntValueRepresentableImplSToU<TTo, TFrom>(TFrom value)
        where TTo : IMinMaxValue<TTo>, INumber<TTo>
        where TFrom : IMinMaxValue<TFrom>, INumber<TFrom>, IComparisonOperators<TFrom, TFrom, bool>
    {
        if (value < TFrom.Zero)
        {
            return false;
        }

        return IsIntValueRepresentableImplUToU<TTo, TFrom>(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIntValueRepresentableImplUToS<TTo, TFrom>(TFrom value)
        where TTo : IMinMaxValue<TTo>, INumber<TTo>
        where TFrom : IMinMaxValue<TFrom>, INumber<TFrom>
    {
        return ulong.CreateTruncating(value) <= ulong.CreateTruncating(TTo.MaxValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanAddWithoutOverflow<T>(T x, T y) where T : IMinMaxValue<T>, INumber<T>
    {
        if (y >= T.Zero)
        {
            return x <= T.MaxValue - y;
        }
        else
        {
            return x >= T.MinValue - y;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanSubtractWithoutOverflow<T>(T x, T y) where T : IMinMaxValue<T>, INumber<T>
    {
        if (y >= T.Zero)
        {
            return x >= T.MinValue + y;
        }
        else
        {
            return x <= T.MaxValue + y;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanMultiplyWithoutOverflow<T>(T x, T y) where T : IMinMaxValue<T>, INumber<T>
    {
        if (x == T.Zero || y == T.Zero)
            return true;

        if (x > T.Zero)
        {
            if (y > T.Zero)
            {
                return y <= T.MaxValue / x;
            }
            else
            {
                return y >= T.MinValue / x;
            }
        }
        else
        {
            if (y > T.Zero)
            {
                return x >= T.MinValue / y;
            }
            else
            {
                return y >= T.MaxValue / x;
            }
        }
    }

    public static bool TryAddWithoutOverflow<T>(out T value, T x, T y) where T : IMinMaxValue<T>, INumber<T>
    {
        if (CanAddWithoutOverflow(x, y))
        {
            value = x + y;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    public static bool TrySubtractWithoutOverflow<T>(out T value, T x, T y) where T : IMinMaxValue<T>, INumber<T>
    {
        if (CanSubtractWithoutOverflow(x, y))
        {
            value = x - y;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    public static bool TryMultiplyWithoutOverflow<T>(out T value, T x, T y) where T : IMinMaxValue<T>, INumber<T>
    {
        if (CanMultiplyWithoutOverflow(x, y))
        {
            value = x * y;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }
}