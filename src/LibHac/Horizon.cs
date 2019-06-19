using LibHac.Fs;
using LibHac.Fs.Accessors;

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

        public Horizon(IAccessLogger logger, ITimeSpanGenerator timer)
        {
            Time = timer;

            Fs = new FileSystemManager(this, logger, timer);
        }
    }
}
