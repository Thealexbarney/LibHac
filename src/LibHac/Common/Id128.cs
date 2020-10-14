using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Util;

namespace LibHac.Common
{
    /// <summary>
    /// A generic 128-bit ID value.
    /// </summary>
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct Id128 : IEquatable<Id128>, IComparable<Id128>, IComparable
    {
        public readonly ulong High;
        public readonly ulong Low;

        public static Id128 Zero => default;

        public Id128(ulong high, ulong low)
        {
            High = high;
            Low = low;
        }

        public Id128(ReadOnlySpan<byte> uid)
        {
            ReadOnlySpan<ulong> longs = MemoryMarshal.Cast<byte, ulong>(uid);

            High = longs[0];
            Low = longs[1];
        }

        public override string ToString() => AsBytes().ToHexString();

        public bool Equals(Id128 other)
        {
            return High == other.High && Low == other.Low;
        }

        public override bool Equals(object obj)
        {
            return obj is Id128 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(High, Low);
        }

        public int CompareTo(Id128 other)
        {
            int highComparison = High.CompareTo(other.High);
            if (highComparison != 0) return highComparison;
            return Low.CompareTo(other.Low);
        }

        public int CompareTo(object obj)
        {
            if (obj is null) return 1;
            return obj is Id128 other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Id128)}");
        }

        public readonly void ToBytes(Span<byte> output)
        {
            Span<ulong> longs = MemoryMarshal.Cast<byte, ulong>(output);

            longs[0] = High;
            longs[1] = Low;
        }

        public ReadOnlySpan<byte> AsBytes()
        {
            return SpanHelpers.AsByteSpan(ref this);
        }

        public static bool operator ==(Id128 left, Id128 right) => left.Equals(right);
        public static bool operator !=(Id128 left, Id128 right) => !left.Equals(right);

        public static bool operator <(Id128 left, Id128 right) => left.CompareTo(right) < 0;
        public static bool operator >(Id128 left, Id128 right) => left.CompareTo(right) > 0;
        public static bool operator <=(Id128 left, Id128 right) => left.CompareTo(right) <= 0;
        public static bool operator >=(Id128 left, Id128 right) => left.CompareTo(right) >= 0;
    }
}
