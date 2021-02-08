using System.Diagnostics;

namespace LibHac
{
    public class StopWatchTimeSpanGenerator : ITimeSpanGenerator
    {
        private Stopwatch Timer = Stopwatch.StartNew();

        public System.TimeSpan GetCurrent()
        {
            return Timer.Elapsed;
        }
    }
}
