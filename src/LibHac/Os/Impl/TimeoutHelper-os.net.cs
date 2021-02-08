using System;

namespace LibHac.Os.Impl
{
    internal static class TimeoutHelperImpl
    {
        public static void Sleep(OsState os, TimeSpan time)
        {
            if (time == new TimeSpan(0))
                return;

            TickManager tickManager = os.GetTickManager();

            // Attempt to avoid overflow by doing the addition unsigned
            ulong currentTick = (ulong)tickManager.GetTick().GetInt64Value();
            ulong timeoutTick = (ulong)tickManager.ConvertToTick(time).GetInt64Value();
            ulong absoluteEndTick = currentTick + timeoutTick + 1;

            var endTick = new Tick((long)Math.Min(long.MaxValue, absoluteEndTick));

            Tick curTick = tickManager.GetTick();

            // Sleep in a loop until the requested time has past.
            while (curTick < endTick)
            {
                Tick remaining = endTick - curTick;
                int sleepTimeMs = (int)ConvertToImplTime(os, remaining).GetMilliSeconds();

                System.Threading.Thread.Sleep(sleepTimeMs);

                curTick = tickManager.GetTick();
            }
        }

        public static TimeSpan ConvertToImplTime(OsState os, Tick tick)
        {
            TickManager tickManager = os.GetTickManager();
            TimeSpan ts = tickManager.ConvertToTimespan(tick);

            // .NET allows sleeping up to int.MaxValue milliseconds at a time.
            long timeMs = Math.Min(int.MaxValue, ts.GetMilliSeconds());
            return TimeSpan.FromMilliSeconds(timeMs);
        }
    }
}
