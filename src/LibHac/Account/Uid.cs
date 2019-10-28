using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Account
{
    [DebuggerDisplay("0x{ToString(),nq}")]
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct Uid : IEquatable<Uid>, IComparable<Uid>, IComparable
    {
        public static Uid Zero => default;

        public readonly Id128 Id;

        public Uid(ulong high, ulong low)
        {
            Id = new Id128(high, low);
        }

        public Uid(ReadOnlySpan<byte> uid)
        {
            Id = new Id128(uid);
        }

        public override string ToString()
        {
            return $"{Id.High:X16}{Id.Low:X16}";
        }

        public bool Equals(Uid other) => Id == other.Id;
        public override bool Equals(object obj) => obj is Uid other && Equals(other);

        public override int GetHashCode() => Id.GetHashCode();

        public int CompareTo(Uid other) => Id.CompareTo(other.Id);

        public int CompareTo(object obj)
        {
            if (obj is null) return 1;
            return obj is Uid other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Uid)}");
        }

        public void ToBytes(Span<byte> output) => Id.ToBytes(output);

        public ReadOnlySpan<byte> AsBytes()
        {
            return SpanHelpers.AsByteSpan(ref this);
        }

        public static bool operator ==(Uid left, Uid right) => left.Equals(right);
        public static bool operator !=(Uid left, Uid right) => !left.Equals(right);

        public static bool operator <(Uid left, Uid right) => left.CompareTo(right) < 0;
        public static bool operator >(Uid left, Uid right) => left.CompareTo(right) > 0;
        public static bool operator <=(Uid left, Uid right) => left.CompareTo(right) <= 0;
        public static bool operator >=(Uid left, Uid right) => left.CompareTo(right) >= 0;
    }
}
