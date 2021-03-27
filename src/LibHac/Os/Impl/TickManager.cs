using System;
using LibHac.Diag;

namespace LibHac.Os.Impl
{
    internal static class TickManagerApi
    {
        public static TickManager GetTickManager(this OsState os) => os.GetOsResourceManager().TickManager;
        public static Tick GetCurrentTick(this OsState os) => os.GetTickManager().GetTick();
        public static Tick GetCurrentTickOrdered(this OsState os) => os.GetTickManager().GetSystemTickOrdered();
    }

    public class TickManager : IDisposable
    {
        private static readonly long MaxTickFrequency = long.MaxValue / TimeSpan.FromSeconds(1).GetNanoSeconds() - 1;

        private TickManagerImpl _impl;

        public TickManager(ITickGenerator tickGenerator) => _impl = new TickManagerImpl(tickGenerator);

        ~TickManager()
        {
            Dispose();
        }

        public void Dispose()
        {
            _impl.Dispose();
            GC.SuppressFinalize(this);
        }

        public Tick GetTick() => _impl.GetTick();
        public Tick GetSystemTickOrdered() => _impl.GetSystemTickOrdered();
        public long GetTickFrequency() => _impl.GetTickFrequency();
        public long GetMaxTick() => _impl.GetMaxTick();
        public long GetMaxTimeSpanNs() => _impl.GetMaxTimeSpanNs();

        public TimeSpan ConvertToTimespan(Tick tick)
        {
            // Get the tick value.
            long ticks = tick.GetInt64Value();

            // Get the tick frequency.
            long tickFreq = GetTickFrequency();
            Assert.SdkLess(tickFreq, MaxTickFrequency);

            // Clamp tick to range.
            if (ticks > GetMaxTick())
            {
                return TimeSpan.FromNanoSeconds(long.MaxValue);
            }
            else if (ticks < -GetMaxTick())
            {
                return TimeSpan.FromNanoSeconds(long.MinValue);
            }
            else
            {
                // Convert to timespan.
                long nanoSecondsPerSecond = TimeSpan.FromSeconds(1).GetNanoSeconds();
                long seconds = ticks / tickFreq;
                long frac = ticks % tickFreq;

                TimeSpan ts = TimeSpan.FromSeconds(seconds) +
                              TimeSpan.FromNanoSeconds(frac * nanoSecondsPerSecond / tickFreq);

                Assert.SdkAssert(!(ticks > 0 && ts < default(TimeSpan) || ticks < 0 && ts > default(TimeSpan)));

                return ts;
            }
        }

        public Tick ConvertToTick(TimeSpan ts)
        {
            // Get the TimeSpan in nanoseconds.
            long ns = ts.GetNanoSeconds();

            // Clamp ns to range.
            if (ns > GetMaxTimeSpanNs())
            {
                return new Tick(long.MaxValue);
            }
            else if (ns < -GetMaxTimeSpanNs())
            {
                return new Tick(long.MinValue);
            }
            else
            {
                // Get the tick frequency.
                long tickFreq = GetTickFrequency();
                Assert.SdkLess(tickFreq, MaxTickFrequency);

                // Convert to tick.
                long nanoSecondsPerSecond = TimeSpan.FromSeconds(1).GetNanoSeconds();
                bool isNegative = ns < 0;
                long seconds = ns / nanoSecondsPerSecond;
                long frac = ns % nanoSecondsPerSecond;

                // If negative, negate seconds/frac.
                if (isNegative)
                {
                    seconds = -seconds;
                    frac = -frac;
                }

                // Calculate the tick, and invert back to negative if needed.
                long ticks = (seconds * tickFreq) +
                            ((frac * tickFreq + nanoSecondsPerSecond - 1) / nanoSecondsPerSecond);

                if (isNegative)
                {
                    ticks = -ticks;
                }

                return new Tick(ticks);
            }
        }
    }
}
