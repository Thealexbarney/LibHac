using LibHac.Bcat;
using LibHac.FsSrv;

namespace LibHac
{
    public static class HorizonFactory
    {
        public static Horizon CreateWithFsConfig(ITimeSpanGenerator timer, FileSystemServerConfig fsServerConfig)
        {
            var horizon = new Horizon(timer);

            HorizonClient fsServerClient = horizon.CreatePrivilegedHorizonClient();
            var fsServer = new FileSystemServer(fsServerClient);

            FileSystemServerInitializer.InitializeWithConfig(fsServerClient, fsServer, fsServerConfig);

            HorizonClient bcatServerClient = horizon.CreateHorizonClient();
            _ = new BcatServer(bcatServerClient);

            return horizon;
        }
    }
}
