using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Util;

namespace LibHac.Diag.Impl;

internal static class AssertImpl
{
    internal static void InvokeAssertionNotNull(AssertionType assertionType, string valueText, string functionName,
        string fileName, int lineNumber)
    {
        Assert.OnAssertionFailure(assertionType, "NotNull", functionName, fileName, lineNumber,
            $"{valueText} must not be nullptr.");
    }

    internal static void InvokeAssertionNull(AssertionType assertionType, string valueText, string functionName,
        string fileName, int lineNumber)
    {
        Assert.OnAssertionFailure(assertionType, "Null", functionName, fileName, lineNumber,
            $"{valueText} must be nullptr.");
    }

    internal static void InvokeAssertionInRange<T>(AssertionType assertionType, T value, T lower, T upper,
        string valueText, string lowerText, string upperText, string functionName, string fileName, int lineNumber)
        where T : IComparisonOperators<T, T, bool>
    {
        string message =
            string.Format(
                "{0} must be within the range [{1}, {2})\nThe actual values are as follows.\n{0}: {3}\n{1}: {4}\n{2}: {5}",
                valueText, lowerText, upperText, value, lower, upper);

        Assert.OnAssertionFailure(assertionType, "RangeCheck", functionName, fileName, lineNumber, message);
    }

    internal static void InvokeAssertionWithinMinMax<T>(AssertionType assertionType, T value, T min, T max,
        string valueText, string minText, string maxText, string functionName, string fileName, int lineNumber)
        where T : IComparisonOperators<T, T, bool>
    {
        string message =
            string.Format(
                "{0} must satisfy the condition min:{1} and max:{2}\nThe actual values are as follows.\n{0}: {3}\n{1}: {4}\n{2}: {5}",
                valueText, minText, maxText, value, min, max);

        Assert.OnAssertionFailure(assertionType, "MinMaxCheck", functionName, fileName, lineNumber, message);
    }

    internal static void InvokeAssertionEqual<T>(AssertionType assertionType, T lhs, T rhs, string lhsText,
        string rhsText, string functionName, string fileName, int lineNumber) where T : IEquatable<T>
    {
        string message =
            string.Format("{0} must be equal to {1}.\nThe actual values are as follows.\n{0}: {2}\n{1}: {3}",
                lhsText, rhsText, lhs, rhs);

        Assert.OnAssertionFailure(assertionType, "Equal", functionName, fileName, lineNumber, message);
    }

    internal static void InvokeAssertionNotEqual<T>(AssertionType assertionType, T lhs, T rhs, string lhsText,
        string rhsText, string functionName, string fileName, int lineNumber) where T : IEquatable<T>
    {
        string message =
            string.Format("{0} must be equal to {1}.\nThe actual values are as follows.\n{0}: {2}\n{1}: {3}",
                lhsText, rhsText, lhs, rhs);

        Assert.OnAssertionFailure(assertionType, "Equal", functionName, fileName, lineNumber, message);
    }

    internal static void InvokeAssertionLess<T>(AssertionType assertionType, T lhs, T rhs, string lhsText,
        string rhsText, string functionName, string fileName, int lineNumber) where T : IComparable<T>
    {
        string message =
            string.Format("{0} must be less than {1}.\nThe actual values are as follows.\n{0}: {2}\n{1}: {3}",
                lhsText, rhsText, lhs, rhs);

        Assert.OnAssertionFailure(assertionType, "Less", functionName, fileName, lineNumber, message);
    }

    internal static void InvokeAssertionLessEqual<T>(AssertionType assertionType, T lhs, T rhs, string lhsText,
        string rhsText, string functionName, string fileName, int lineNumber) where T : IComparable<T>
    {
        string message =
            string.Format(
                "{0} must be less than or equal to {1}.\nThe actual values are as follows.\n{0}: {2}\n{1}: {3}",
                lhsText, rhsText, lhs, rhs);

        Assert.OnAssertionFailure(assertionType, "LessEqual", functionName, fileName, lineNumber, message);
    }

    internal static void InvokeAssertionGreater<T>(AssertionType assertionType, T lhs, T rhs, string lhsText,
        string rhsText, string functionName, string fileName, int lineNumber) where T : IComparable<T>
    {
        string message =
            string.Format("{0} must be greater than {1}.\nThe actual values are as follows.\n{0}: {2}\n{1}: {3}",
                lhsText, rhsText, lhs, rhs);

        Assert.OnAssertionFailure(assertionType, "Greater", functionName, fileName, lineNumber, message);
    }

    internal static void InvokeAssertionGreaterEqual<T>(AssertionType assertionType, T lhs, T rhs, string lhsText,
        string rhsText, string functionName, string fileName, int lineNumber) where T : IComparable<T>
    {
        string message =
            string.Format(
                "{0} must be greater than or equal to {1}.\nThe actual values are as follows.\n{0}: {2}\n{1}: {3}",
                lhsText, rhsText, lhs, rhs);

        Assert.OnAssertionFailure(assertionType, "GreaterEqual", functionName, fileName, lineNumber, message);
    }

    internal static void InvokeAssertionAligned<T>(AssertionType assertionType, T value, int alignment,
        string valueText, string alignmentText, string functionName, string fileName, int lineNumber)
    {
        string message =
            string.Format("{0} must be {1}-bytes aligned.\nThe actual values are as follows.\n{0}: {2}\n{1}: {3}",
                valueText, alignmentText, value, alignment);

        Assert.OnAssertionFailure(assertionType, "Aligned", functionName, fileName, lineNumber, message);
    }

    public static bool Null<T>(T item) where T : class
    {
        return item is null;
    }

    public static bool Null<T>(ref T item)
    {
        return Unsafe.IsNullRef(ref item);
    }

    public static bool Null<T>(in UniqueRef<T> item) where T : class, IDisposable
    {
        return !item.HasValue;
    }

    public static bool Null<T>(in SharedRef<T> item) where T : class, IDisposable
    {
        return !item.HasValue;
    }

    public static bool NotNull<T>(T item) where T : class
    {
        return item is not null;
    }

    public static bool NotNull<T>(ref T item)
    {
        return !Unsafe.IsNullRef(ref item);
    }

    public static bool NotNull<T>(Span<T> span)
    {
        return !Unsafe.IsNullRef(ref MemoryMarshal.GetReference(span)) || span.Length == 0;
    }

    public static bool NotNull<T>(ReadOnlySpan<T> span)
    {
        return !Unsafe.IsNullRef(ref MemoryMarshal.GetReference(span)) || span.Length == 0;
    }

    public static bool NotNull<T>(in UniqueRef<T> item) where T : class, IDisposable
    {
        return item.HasValue;
    }

    public static bool NotNull<T>(in SharedRef<T> item) where T : class, IDisposable
    {
        return item.HasValue;
    }

    public static bool WithinRange<T>(T value, T lowerInclusive, T upperExclusive)
        where T : IComparisonOperators<T, T, bool>
    {
        return lowerInclusive <= value && value < upperExclusive;
    }

    public static bool WithinMinMax<T>(T value, T min, T max) where T : IComparisonOperators<T, T, bool>
    {
        return min <= value && value <= max;
    }

    public static bool Equal<T>(T lhs, T rhs) where T : IEquatable<T>
    {
        return lhs.Equals(rhs);
    }

    public static bool Equal<T>(ref T lhs, ref T rhs) where T : IEquatable<T>
    {
        return lhs.Equals(rhs);
    }

    public static bool NotEqual<T>(T lhs, T rhs) where T : IEquatable<T>
    {
        return !lhs.Equals(rhs);
    }

    public static bool NotEqual<T>(ref T lhs, ref T rhs) where T : IEquatable<T>
    {
        return !lhs.Equals(rhs);
    }

    public static bool Less<T>(T lhs, T rhs) where T : IComparable<T>
    {
        return lhs.CompareTo(rhs) < 0;
    }

    public static bool Less<T>(ref T lhs, ref T rhs) where T : IComparable<T>
    {
        return lhs.CompareTo(rhs) < 0;
    }

    public static bool LessEqual<T>(T lhs, T rhs) where T : IComparable<T>
    {
        return lhs.CompareTo(rhs) <= 0;
    }

    public static bool LessEqual<T>(ref T lhs, ref T rhs) where T : IComparable<T>
    {
        return lhs.CompareTo(rhs) <= 0;
    }

    public static bool Greater<T>(T lhs, T rhs) where T : IComparable<T>
    {
        return lhs.CompareTo(rhs) > 0;
    }

    public static bool Greater<T>(ref T lhs, ref T rhs) where T : IComparable<T>
    {
        return lhs.CompareTo(rhs) > 0;
    }

    public static bool GreaterEqual<T>(T lhs, T rhs) where T : IComparable<T>
    {
        return lhs.CompareTo(rhs) >= 0;
    }

    public static bool GreaterEqual<T>(ref T lhs, ref T rhs) where T : IComparable<T>
    {
        return lhs.CompareTo(rhs) >= 0;
    }

    public static bool IsAligned<T>(T value, int alignment) where T : IBinaryNumber<T>
    {
        return Alignment.IsAligned(value, (uint)alignment);
    }
}