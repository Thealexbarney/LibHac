using System;
using System.Runtime.InteropServices;

namespace LibHac.Fs
{
    public struct UserId : IEquatable<UserId>, IComparable<UserId>, IComparable
    {
        public readonly ulong High;
        public readonly ulong Low;

        public UserId(ulong high, ulong low)
        {
            High = high;
            Low = low;
        }

        public UserId(ReadOnlySpan<byte> uid)
        {
            ReadOnlySpan<ulong> longs = MemoryMarshal.Cast<byte, ulong>(uid);

            High = longs[0];
            Low = longs[1];
        }

        public bool Equals(UserId other)
        {
            return High == other.High && Low == other.Low;
        }

        public override bool Equals(object obj)
        {
            return obj is UserId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (High.GetHashCode() * 397) ^ Low.GetHashCode();
            }
        }

        public int CompareTo(UserId other)
        {
            int highComparison = High.CompareTo(other.High);
            if (highComparison != 0) return highComparison;
            return Low.CompareTo(other.Low);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            return obj is UserId other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(UserId)}");
        }

        public void ToBytes(Span<byte> output)
        {
            Span<ulong> longs = MemoryMarshal.Cast<byte, ulong>(output);

            longs[0] = High;
            longs[1] = Low;
        }
    }
}
