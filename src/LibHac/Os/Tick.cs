using System;
using LibHac.Os.Impl;

namespace LibHac.Os
{
    public readonly struct Tick : IEquatable<Tick>
    {
        private readonly long _ticks;

        public Tick(long ticks) => _ticks = ticks;

        public long GetInt64Value() => _ticks;
        public TimeSpan ToTimeSpan(OsState os) => os.ConvertToTimeSpan(this);

        public static Tick operator +(Tick left, Tick right) => new(left._ticks + right._ticks);
        public static Tick operator -(Tick left, Tick right) => new(left._ticks - right._ticks);

        public static bool operator ==(Tick left, Tick right) => left._ticks == right._ticks;
        public static bool operator !=(Tick left, Tick right) => left._ticks != right._ticks;
        public static bool operator <(Tick left, Tick right) => left._ticks < right._ticks;
        public static bool operator >(Tick left, Tick right) => left._ticks > right._ticks;
        public static bool operator <=(Tick left, Tick right) => left._ticks <= right._ticks;
        public static bool operator >=(Tick left, Tick right) => left._ticks >= right._ticks;

        public override bool Equals(object obj) => obj is Tick other && Equals(other);
        public bool Equals(Tick other) => _ticks == other._ticks;
        public override int GetHashCode() => _ticks.GetHashCode();
        public override string ToString() => _ticks.ToString();
    }

    public static class TickApi
    {
        public static Tick GetSystemTick(this OsState os) => os.GetTickManager().GetTick();
        public static Tick GetSystemTickOrdered(this OsState os) => os.GetTickManager().GetSystemTickOrdered();
        public static long GetSystemTickFrequency(this OsState os) => os.GetTickManager().GetTickFrequency();
        public static TimeSpan ConvertToTimeSpan(this OsState os, Tick tick) => os.GetTickManager().ConvertToTimespan(tick);
        public static Tick ConvertToTick(this OsState os, TimeSpan ts) => os.GetTickManager().ConvertToTick(ts);
    }
}
