using LibHac.Fs;
using LibHac.FsService;
using LibHac.FsService.Creators;

namespace LibHac
{
    public class Horizon
    {
        internal ITimeSpanGenerator Time { get; }

        public FileSystemClient Fs { get; }
        public FileSystemServer FsSrv { get; private set; }

        private readonly object _initLocker = new object();

        public Horizon(ITimeSpanGenerator timer)
        {
            Time = timer;

            Fs = new FileSystemClient(timer);
        }

        public void InitializeFileSystemServer(FileSystemCreators fsCreators, IDeviceOperator deviceOperator)
        {
            if (FsSrv != null) return;

            lock (_initLocker)
            {
                if (FsSrv != null) return;

                var config = new FileSystemServerConfig();
                config.FsCreators = fsCreators;
                config.DeviceOperator = deviceOperator;

                FsSrv = new FileSystemServer(config);
            }
        }
    }
}
