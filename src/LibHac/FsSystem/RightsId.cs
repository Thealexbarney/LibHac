using System;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.FsSystem
{
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct RightsId : IEquatable<RightsId>, IComparable<RightsId>, IComparable
    {
        public readonly Id128 Id;

        public RightsId(ulong high, ulong low)
        {
            Id = new Id128(high, low);
        }

        public RightsId(ReadOnlySpan<byte> uid)
        {
            Id = new Id128(uid);
        }

        public bool Equals(RightsId other) => Id == other.Id;
        public override bool Equals(object obj) => obj is RightsId other && Equals(other);

        public override int GetHashCode() => Id.GetHashCode();

        // ReSharper disable once ImpureMethodCallOnReadonlyValueField
        public int CompareTo(RightsId other) => Id.CompareTo(other.Id);

        public int CompareTo(object obj)
        {
            if (obj is null) return 1;
            return obj is RightsId other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(RightsId)}");
        }

        // ReSharper disable once ImpureMethodCallOnReadonlyValueField
        public void ToBytes(Span<byte> output) => Id.ToBytes(output);

        public static bool operator ==(RightsId left, RightsId right) => left.Equals(right);
        public static bool operator !=(RightsId left, RightsId right) => !left.Equals(right);

        public static bool operator <(RightsId left, RightsId right) => left.CompareTo(right) < 0;
        public static bool operator >(RightsId left, RightsId right) => left.CompareTo(right) > 0;
        public static bool operator <=(RightsId left, RightsId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(RightsId left, RightsId right) => left.CompareTo(right) >= 0;
    }
}