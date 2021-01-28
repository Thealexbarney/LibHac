using System;

namespace LibHac.Os.Impl
{
    internal readonly struct TimeoutHelper
    {
        private readonly Tick _endTick;
        private readonly OsState _os;

        public TimeoutHelper(OsState os, TimeSpan timeout)
        {
            _os = os;

            if (timeout == new TimeSpan(0))
            {
                // If timeout is zero, don't do relative tick calculations.
                _endTick = new Tick(0);
            }
            else
            {
                TickManager tickManager = os.GetTickManager();

                // Attempt to avoid overflow by doing the addition unsigned
                ulong currentTick = (ulong)tickManager.GetTick().GetInt64Value();
                ulong timeoutTick = (ulong)tickManager.ConvertToTick(timeout).GetInt64Value();
                ulong absoluteEndTick = currentTick + timeoutTick + 1;

                _endTick = new Tick((long)Math.Min(long.MaxValue, absoluteEndTick));
            }
        }

        public bool TimedOut()
        {
            if (_endTick.GetInt64Value() == 0)
                return true;

            Tick currentTick = _os.GetTickManager().GetTick();

            return currentTick >= _endTick;
        }

        public TimeSpan GetTimeLeftOnTarget()
        {
            // If the end tick is zero, we're expired.
            if (_endTick.GetInt64Value() == 0)
                return new TimeSpan(0);

            // Check if we've expired.
            Tick currentTick = _os.GetTickManager().GetTick();
            if (currentTick >= _endTick)
                return new TimeSpan(0);

            // Return the converted difference as a timespan.
            return TimeoutHelperImpl.ConvertToImplTime(_os, _endTick - currentTick);
        }

        public static void Sleep(OsState os, TimeSpan timeout)
        {
            TimeoutHelperImpl.Sleep(os, timeout);
        }
    }
}
