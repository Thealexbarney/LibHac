using System;
using System.Runtime.InteropServices;

namespace LibHac.Common
{
    /// <summary>
    /// A generic 128-bit ID value.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct Id128 : IEquatable<Id128>, IComparable<Id128>, IComparable
    {
        public readonly ulong High;
        public readonly ulong Low;

        public static readonly Id128 InvalidId = new Id128(0, 0);

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
            unchecked
            {
                return (High.GetHashCode() * 397) ^ Low.GetHashCode();
            }
        }

        public int CompareTo(Id128 other)
        {
            // ReSharper disable ImpureMethodCallOnReadonlyValueField
            int highComparison = High.CompareTo(other.High);
            if (highComparison != 0) return highComparison;
            return Low.CompareTo(other.Low);
            // ReSharper restore ImpureMethodCallOnReadonlyValueField
        }

        public int CompareTo(object obj)
        {
            if (obj is null) return 1;
            return obj is Id128 other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Id128)}");
        }

        public void ToBytes(Span<byte> output)
        {
            Span<ulong> longs = MemoryMarshal.Cast<byte, ulong>(output);

            longs[0] = High;
            longs[1] = Low;
        }

        public static bool operator ==(Id128 left, Id128 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Id128 left, Id128 right)
        {
            return !left.Equals(right);
        }
        public static bool operator <(Id128 left, Id128 right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(Id128 left, Id128 right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(Id128 left, Id128 right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(Id128 left, Id128 right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}
