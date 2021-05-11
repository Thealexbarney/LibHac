using System;

namespace LibHac.Time
{
    public readonly struct PosixTime : IComparable<PosixTime>, IEquatable<PosixTime>
    {
        public readonly long Value;

        public PosixTime(long value)
        {
            Value = value;
        }

        public static PosixTime operator +(PosixTime left, TimeSpan right) =>
            new PosixTime(left.Value + right.GetSeconds());

        public static PosixTime operator -(PosixTime left, TimeSpan right) =>
            new PosixTime(left.Value - right.GetSeconds());

        public static TimeSpan operator -(PosixTime left, PosixTime right) =>
            TimeSpan.FromSeconds(left.Value - right.Value);

        public static bool operator ==(PosixTime left, PosixTime right) => left.Value == right.Value;
        public static bool operator !=(PosixTime left, PosixTime right) => left.Value != right.Value;
        public static bool operator <=(PosixTime left, PosixTime right) => left.Value <= right.Value;
        public static bool operator >=(PosixTime left, PosixTime right) => left.Value >= right.Value;
        public static bool operator <(PosixTime left, PosixTime right) => left.Value < right.Value;
        public static bool operator >(PosixTime left, PosixTime right) => left.Value > right.Value;

        public bool Equals(PosixTime other) => Value == other.Value;
        public int CompareTo(PosixTime other) => Value.CompareTo(other.Value);

        public override bool Equals(object obj) => obj is PosixTime time && Equals(time);
        public override int GetHashCode() => Value.GetHashCode();
    }
}