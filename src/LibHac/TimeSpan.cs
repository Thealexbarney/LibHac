using System;

namespace LibHac
{
    public readonly struct TimeSpan : IEquatable<TimeSpan>, IComparable<TimeSpan>
    {
        private readonly TimeSpanType _ts;

        public TimeSpan(nint _) => _ts = new TimeSpanType();
        public TimeSpan(TimeSpanType ts) => _ts = ts;

        public static TimeSpan FromNanoSeconds(long nanoSeconds) => new(TimeSpanType.FromNanoSeconds(nanoSeconds));
        public static TimeSpan FromMicroSeconds(long microSeconds) => new(TimeSpanType.FromMicroSeconds(microSeconds));
        public static TimeSpan FromMilliSeconds(long milliSeconds) => new(TimeSpanType.FromMilliSeconds(milliSeconds));
        public static TimeSpan FromSeconds(long seconds) => new(TimeSpanType.FromSeconds(seconds));
        public static TimeSpan FromMinutes(long minutes) => new(TimeSpanType.FromMinutes(minutes));
        public static TimeSpan FromHours(long hours) => new(TimeSpanType.FromHours(hours));
        public static TimeSpan FromDays(long days) => new(TimeSpanType.FromDays(days));

        public long GetNanoSeconds() => _ts.GetNanoSeconds();
        public long GetMicroSeconds() => _ts.GetMicroSeconds();
        public long GetMilliSeconds() => _ts.GetMilliSeconds();
        public long GetSeconds() => _ts.GetSeconds();
        public long GetMinutes() => _ts.GetMinutes();
        public long GetHours() => _ts.GetHours();
        public long GetDays() => _ts.GetDays();

        public static bool operator ==(TimeSpan left, TimeSpan right) => left.Equals(right);
        public static bool operator !=(TimeSpan left, TimeSpan right) => !(left == right);
        public static bool operator <(TimeSpan left, TimeSpan right) => left._ts < right._ts;
        public static bool operator >(TimeSpan left, TimeSpan right) => left._ts > right._ts;
        public static bool operator <=(TimeSpan left, TimeSpan right) => left._ts <= right._ts;
        public static bool operator >=(TimeSpan left, TimeSpan right) => left._ts >= right._ts;

        public static TimeSpan operator +(TimeSpan left, TimeSpan right) => new(left._ts + right._ts);
        public static TimeSpan operator -(TimeSpan left, TimeSpan right) => new(left._ts - right._ts);

        public static implicit operator TimeSpanType(TimeSpan ts) => ts._ts;

        public override bool Equals(object obj) => obj is TimeSpan ts && Equals(ts);
        public bool Equals(TimeSpan other) => _ts == other._ts;
        public override int GetHashCode() => _ts.GetHashCode();
        public int CompareTo(TimeSpan other) => _ts.CompareTo(other._ts);
        public override string ToString() => _ts.ToString();
    }

    public readonly struct TimeSpanType : IEquatable<TimeSpanType>, IComparable<TimeSpanType>
    {
        private readonly long _nanoSeconds;

        private TimeSpanType(long nanoSeconds) => _nanoSeconds = nanoSeconds;

        public static TimeSpanType FromNanoSeconds(long nanoSeconds) => new(nanoSeconds);
        public static TimeSpanType FromMicroSeconds(long microSeconds) => new(microSeconds * 1000L);
        public static TimeSpanType FromMilliSeconds(long milliSeconds) => new(milliSeconds * (1000L * 1000));
        public static TimeSpanType FromSeconds(long seconds) => new(seconds * (1000L * 1000 * 1000));
        public static TimeSpanType FromMinutes(long minutes) => new(minutes * (1000L * 1000 * 1000 * 60));
        public static TimeSpanType FromHours(long hours) => new(hours * (1000L * 1000 * 1000 * 60 * 60));
        public static TimeSpanType FromDays(long days) => new(days * (1000L * 1000 * 1000 * 60 * 60 * 24));

        public long GetNanoSeconds() => _nanoSeconds;
        public long GetMicroSeconds() => _nanoSeconds / 1000;
        public long GetMilliSeconds() => _nanoSeconds / (1000L * 1000);
        public long GetSeconds() => _nanoSeconds / (1000L * 1000 * 1000);
        public long GetMinutes() => _nanoSeconds / (1000L * 1000 * 1000 * 60);
        public long GetHours() => _nanoSeconds / (1000L * 1000 * 1000 * 60 * 60);
        public long GetDays() => _nanoSeconds / (1000L * 1000 * 1000 * 60 * 60 * 24);

        public static bool operator ==(TimeSpanType left, TimeSpanType right) => left._nanoSeconds == right._nanoSeconds;
        public static bool operator !=(TimeSpanType left, TimeSpanType right) => left._nanoSeconds != right._nanoSeconds;
        public static bool operator <(TimeSpanType left, TimeSpanType right) => left._nanoSeconds < right._nanoSeconds;
        public static bool operator >(TimeSpanType left, TimeSpanType right) => left._nanoSeconds > right._nanoSeconds;
        public static bool operator <=(TimeSpanType left, TimeSpanType right) => left._nanoSeconds <= right._nanoSeconds;
        public static bool operator >=(TimeSpanType left, TimeSpanType right) => left._nanoSeconds >= right._nanoSeconds;

        public static TimeSpanType operator +(TimeSpanType left, TimeSpanType right) => new(left._nanoSeconds + right._nanoSeconds);
        public static TimeSpanType operator -(TimeSpanType left, TimeSpanType right) => new(left._nanoSeconds - right._nanoSeconds);

        public override bool Equals(object obj) => obj is TimeSpanType ts && Equals(ts);
        public bool Equals(TimeSpanType other) => _nanoSeconds == other._nanoSeconds;
        public override int GetHashCode() => (int)_nanoSeconds;
        public int CompareTo(TimeSpanType other) => _nanoSeconds.CompareTo(other._nanoSeconds);
        public override string ToString() => _nanoSeconds.ToString();
    }
}
