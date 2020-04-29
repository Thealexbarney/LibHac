using LibHac.Common;
using LibHac.Fs;
using LibHac.FsService;
using LibHac.FsService.Creators;
using LibHac.Sm;

namespace LibHac
{
    public class Horizon
    {
        internal ITimeSpanGenerator Time { get; }
        private FileSystemServer FileSystemServer { get; set; }
        internal ServiceManager ServiceManager { get; }

        private readonly object _initLocker = new object();

        public Horizon(ITimeSpanGenerator timer)
        {
            Time = timer ?? new StopWatchTimeSpanGenerator();
            ServiceManager = new ServiceManager(this);
        }

        public Horizon(ITimeSpanGenerator timer, FileSystemServer fsServer)
        {
            Time = timer ?? new StopWatchTimeSpanGenerator();
            FileSystemServer = fsServer;
            ServiceManager = new ServiceManager(this);
        }

        private Result OpenFileSystemClient(out FileSystemClient client)
        {
            if (FileSystemServer is null)
            {
                client = default;
                return ResultLibHac.ServiceNotInitialized.Log();
            }

            client = FileSystemServer.CreateFileSystemClient();
            return Result.Success;
        }

        public Result CreateHorizonClient(out HorizonClient client)
        {
            Result rc = OpenFileSystemClient(out FileSystemClient fsClient);
            if (rc.IsFailure())
            {
                client = default;
                return rc;
            }

            client = new HorizonClient(this, fsClient);
            return Result.Success;
        }

        public void InitializeFileSystemServer(FileSystemCreators fsCreators, IDeviceOperator deviceOperator)
        {
            if (FileSystemServer != null) return;

            lock (_initLocker)
            {
                if (FileSystemServer != null) return;

                var config = new FileSystemServerConfig();
                config.FsCreators = fsCreators;
                config.DeviceOperator = deviceOperator;

                FileSystemServer = new FileSystemServer(config);
            }
        }
    }
}
