using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Fs
{
    [DebuggerDisplay("0x{ToString(),nq}")]
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct UserId : IEquatable<UserId>, IComparable<UserId>, IComparable
    {
        public static UserId InvalidId => default;

        public readonly Id128 Id;

        public UserId(ulong high, ulong low)
        {
            Id = new Id128(high, low);
        }

        public UserId(ReadOnlySpan<byte> uid)
        {
            Id = new Id128(uid);
        }

        public override string ToString()
        {
            return $"{Id.High:X16}{Id.Low:X16}";
        }

        public bool Equals(UserId other) => Id == other.Id;
        public override bool Equals(object obj) => obj is UserId other && Equals(other);

        public override int GetHashCode() => Id.GetHashCode();

        public int CompareTo(UserId other) => Id.CompareTo(other.Id);

        public int CompareTo(object obj)
        {
            if (obj is null) return 1;
            return obj is UserId other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(UserId)}");
        }

        public void ToBytes(Span<byte> output) => Id.ToBytes(output);

        public ReadOnlySpan<byte> AsBytes()
        {
            return SpanHelpers.AsByteSpan(ref this);
        }

        public static bool operator ==(UserId left, UserId right) => left.Equals(right);
        public static bool operator !=(UserId left, UserId right) => !left.Equals(right);

        public static bool operator <(UserId left, UserId right) => left.CompareTo(right) < 0;
        public static bool operator >(UserId left, UserId right) => left.CompareTo(right) > 0;
        public static bool operator <=(UserId left, UserId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(UserId left, UserId right) => left.CompareTo(right) >= 0;
    }
}
