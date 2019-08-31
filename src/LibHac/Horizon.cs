using LibHac.FsClient;
using LibHac.FsService;
using LibHac.FsService.Creators;

namespace LibHac
{
    public class Horizon
    {
        internal ITimeSpanGenerator Time { get; }

        public FileSystemManager Fs { get; }
        public FileSystemServer FsSrv { get; private set; }

        private readonly object _initLocker = new object();

        public Horizon()
        {
            Fs = new FileSystemManager(this);
        }

        public Horizon(ITimeSpanGenerator timer)
        {
            Time = timer;

            Fs = new FileSystemManager(this, timer);
        }

        public void InitializeFileSystemServer(FileSystemCreators fsCreators)
        {
            if (FsSrv != null) return;

            lock (_initLocker)
            {
                if (FsSrv != null) return;
                FsSrv = new FileSystemServer(fsCreators);
            }
        }
    }
}
