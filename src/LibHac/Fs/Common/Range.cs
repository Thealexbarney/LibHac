// ReSharper disable InconsistentNaming CheckNamespace

using System;
using LibHac.Diag;

namespace LibHac.Fs;

/// <summary>
/// Represents a contiguous range of offsets in a piece of data.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public readonly struct Range : IEquatable<Range>, IComparable<Range>
{
    public readonly long Offset;
    public readonly long Size;

    public Range(long offset, long size)
    {
        Offset = offset;
        Size = size;
    }

    public bool Contains(Range range)
    {
        return Offset <= range.Offset &&
               range.Offset + range.Size <= Offset + Size;
    }

    public bool HasIntersection(Range range)
    {
        return Offset + Size > range.Offset &&
               range.Offset + range.Size > Offset;
    }

    public bool IsAdjacent(Range range)
    {
        return Offset + Size == range.Offset ||
               range.Offset + range.Size == Offset;
    }

    public Range MakeMerge(Range range)
    {
        Assert.SdkAssert(HasIntersection(range) || IsAdjacent(range));

        long endOffsetThis = Offset + Size;
        long endOffsetOther = range.Offset + range.Size;

        long startOffsetMerged = Math.Min(Offset, range.Offset);
        long endOffsetMerged = Math.Max(endOffsetThis, endOffsetOther);

        return new Range(startOffsetMerged, endOffsetMerged - startOffsetMerged);
    }

    // Equality is done with only the offset because this type is mainly used with RangeSet
    // which will never contain multiple ranges with the same offset.
    public static bool operator <(Range lhs, Range rhs) => lhs.Offset < rhs.Offset;
    public static bool operator >(Range lhs, Range rhs) => lhs.Offset > rhs.Offset;
    public static bool operator ==(Range lhs, Range rhs) => lhs.Offset == rhs.Offset;
    public static bool operator !=(Range lhs, Range rhs) => lhs.Offset != rhs.Offset;

    public bool Equals(Range other) => Offset == other.Offset;

    public override bool Equals(object obj) => obj is Range other && Equals(other);
    public int CompareTo(Range other) => Offset.CompareTo(other.Offset);
    public override int GetHashCode() => Offset.GetHashCode();
}