using System;
using System.Diagnostics;
using LibHac;

namespace hactoolnet
{
    public class TimeSpanTimer : ITimeSpanGenerator
    {
        private Stopwatch Timer = Stopwatch.StartNew();

        public TimeSpan GetCurrent()
        {
            return Timer.Elapsed;
        }
    }
}
