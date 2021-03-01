using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LibHac.Os.Impl
{
    internal struct TickManagerImpl : IDisposable
    {
        private long _tickFrequency;
        private ITickGenerator _tickGenerator;
        private TimeSpan _maxTimeSpan;
        private long _maxTick;

        public TickManagerImpl(ITickGenerator tickGenerator)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                TimeBeginPeriod(1);
            }

            _tickFrequency = Stopwatch.Frequency;
            _tickGenerator = tickGenerator;

            long nanoSecondsPerSecond = TimeSpan.FromSeconds(1).GetNanoSeconds();

            if (_tickFrequency <= nanoSecondsPerSecond)
            {
                _maxTick = _tickFrequency * (long.MaxValue / nanoSecondsPerSecond);
                _maxTimeSpan = TimeSpan.FromNanoSeconds(long.MaxValue);
            }
            else
            {
                _maxTick = long.MaxValue;
                _maxTimeSpan = TimeSpan.FromSeconds(long.MaxValue / _tickFrequency);
            }
        }

        public void Dispose()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                TimeEndPeriod(1);
            }
        }

        public Tick GetTick() => _tickGenerator.GetCurrentTick();
        public Tick GetSystemTickOrdered() => _tickGenerator.GetCurrentTick();
        public long GetTickFrequency() => _tickFrequency;
        public long GetMaxTick() => _maxTick;
        public long GetMaxTimeSpanNs() => _maxTimeSpan.GetNanoSeconds();


        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint milliseconds);
    }
}
