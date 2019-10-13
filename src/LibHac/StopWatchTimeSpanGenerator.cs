using System;
using System.Diagnostics;

namespace LibHac
{
    public class StopWatchTimeSpanGenerator : ITimeSpanGenerator
    {
        private Stopwatch Timer = Stopwatch.StartNew();

        public TimeSpan GetCurrent()
        {
            return Timer.Elapsed;
        }
    }
}
