using LibHac.Bcat;
using LibHac.Bcat.Detail.Ipc;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsService;
using LibHac.FsService.Creators;

namespace LibHac
{
    public class Horizon
    {
        internal ITimeSpanGenerator Time { get; }
        private FileSystemServer FileSystemServer { get; set; }
        private BcatServer BcatServer { get; set; }

        private readonly object _initLocker = new object();

        public Horizon(ITimeSpanGenerator timer)
        {
            Time = timer;
        }

        public Result OpenFileSystemProxyService(out IFileSystemProxy service)
        {
            if (FileSystemServer is null)
            {
                service = default;
                return ResultLibHac.ServiceNotInitialized.Log();
            }

            service = FileSystemServer.CreateFileSystemProxyService();
            return Result.Success;
        }

        public Result OpenFileSystemClient(out FileSystemClient client)
        {
            if (FileSystemServer is null)
            {
                client = default;
                return ResultLibHac.ServiceNotInitialized.Log();
            }

            client = FileSystemServer.CreateFileSystemClient();
            return Result.Success;
        }

        public Result OpenBcatUService(out IServiceCreator service) => OpenBcatService(out service, BcatServiceType.BcatU);
        public Result OpenBcatSService(out IServiceCreator service) => OpenBcatService(out service, BcatServiceType.BcatS);
        public Result OpenBcatMService(out IServiceCreator service) => OpenBcatService(out service, BcatServiceType.BcatM);
        public Result OpenBcatAService(out IServiceCreator service) => OpenBcatService(out service, BcatServiceType.BcatA);

        private Result OpenBcatService(out IServiceCreator service, BcatServiceType type)
        {
            if (BcatServer is null)
            {
                service = default;
                return ResultLibHac.ServiceNotInitialized.Log();
            }

            return BcatServer.GetServiceCreator(out service, type);
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
