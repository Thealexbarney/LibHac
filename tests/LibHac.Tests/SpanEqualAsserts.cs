using System;
using Xunit.Sdk;

// ReSharper disable once CheckNamespace
namespace Xunit;

public partial class Assert
{
    /// <summary>
    /// Verifies that two spans are equal, using a default comparer.
    /// </summary>
    /// <typeparam name="T">The type of the objects to be compared</typeparam>
    /// <param name="expected">The expected value</param>
    /// <param name="actual">The value to be compared against</param>
    /// <exception cref="EqualException">Thrown when the objects are not equal</exception>
    public static void Equal<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> actual) where T : unmanaged, IEquatable<T>
    {
        if (!expected.SequenceEqual(actual))
            throw new EqualException(expected.ToArray(), actual.ToArray());
    }
}
