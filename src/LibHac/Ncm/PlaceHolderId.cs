using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Ncm
{
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct PlaceHolderId : IEquatable<PlaceHolderId>, IComparable<PlaceHolderId>, IComparable
    {
        public readonly Id128 Id;

        public PlaceHolderId(ulong high, ulong low)
        {
            Id = new Id128(high, low);
        }

        public PlaceHolderId(ReadOnlySpan<byte> uid)
        {
            Id = new Id128(uid);
        }

        public override string ToString() => Id.ToString();

        public bool Equals(PlaceHolderId other) => Id == other.Id;
        public override bool Equals(object obj) => obj is PlaceHolderId other && Equals(other);

        public override int GetHashCode() => Id.GetHashCode();

        public int CompareTo(PlaceHolderId other) => Id.CompareTo(other.Id);

        public int CompareTo(object obj)
        {
            if (obj is null) return 1;
            return obj is PlaceHolderId other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(PlaceHolderId)}");
        }

        public void ToBytes(Span<byte> output) => Id.ToBytes(output);

        public ReadOnlySpan<byte> AsBytes()
        {
            return SpanHelpers.AsByteSpan(ref this);
        }

        public static bool operator ==(PlaceHolderId left, PlaceHolderId right) => left.Equals(right);
        public static bool operator !=(PlaceHolderId left, PlaceHolderId right) => !left.Equals(right);

        public static bool operator <(PlaceHolderId left, PlaceHolderId right) => left.CompareTo(right) < 0;
        public static bool operator >(PlaceHolderId left, PlaceHolderId right) => left.CompareTo(right) > 0;
        public static bool operator <=(PlaceHolderId left, PlaceHolderId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(PlaceHolderId left, PlaceHolderId right) => left.CompareTo(right) >= 0;
    }
}