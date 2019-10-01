using System;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct UserId : IEquatable<UserId>, IComparable<UserId>, IComparable
    {
        public static readonly UserId EmptyId = new UserId(0, 0);

        public readonly Id128 Id;

        public UserId(ulong high, ulong low)
        {
            Id = new Id128(high, low);
        }

        public UserId(ReadOnlySpan<byte> uid)
        {
            Id = new Id128(uid);
        }

        public bool Equals(UserId other) => Id == other.Id;
        public override bool Equals(object obj) => obj is UserId other && Equals(other);

        public override int GetHashCode() => Id.GetHashCode();

        // ReSharper disable once ImpureMethodCallOnReadonlyValueField
        public int CompareTo(UserId other) => Id.CompareTo(other.Id);

        public int CompareTo(object obj)
        {
            if (obj is null) return 1;
            return obj is UserId other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(UserId)}");
        }

        // ReSharper disable once ImpureMethodCallOnReadonlyValueField
        public void ToBytes(Span<byte> output) => Id.ToBytes(output);

        public static bool operator ==(UserId left, UserId right) => left.Equals(right);
        public static bool operator !=(UserId left, UserId right) => !left.Equals(right);

        public static bool operator <(UserId left, UserId right) => left.CompareTo(right) < 0;
        public static bool operator >(UserId left, UserId right) => left.CompareTo(right) > 0;
        public static bool operator <=(UserId left, UserId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(UserId left, UserId right) => left.CompareTo(right) >= 0;
    }
}
