using System.Diagnostics;

namespace LibHac.Os
{
    public interface ITickGenerator
    {
        Tick GetCurrentTick();
    }

    internal class DefaultTickGenerator : ITickGenerator
    {
        private readonly long _initialTick;

        public DefaultTickGenerator()
        {
            _initialTick = Stopwatch.GetTimestamp();
        }

        public Tick GetCurrentTick()
        {
            return new Tick(Stopwatch.GetTimestamp() - _initialTick);
        }
    }
}
