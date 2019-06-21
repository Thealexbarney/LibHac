using LibHac.Fs;

namespace LibHac
{
    public class Horizon
    {
        internal ITimeSpanGenerator Time { get; }

        public FileSystemManager Fs { get; }

        public Horizon()
        {
            Fs = new FileSystemManager(this);
        }

        public Horizon(ITimeSpanGenerator timer)
        {
            Time = timer;

            Fs = new FileSystemManager(this, timer);
        }
    }
}
