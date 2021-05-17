using LibHac.Bcat;
using LibHac.Common.Keys;
using LibHac.Fs.Fsa;
using LibHac.FsSrv;

namespace LibHac
{
    public static class HorizonFactory
    {
        public static Horizon CreateWithDefaultFsConfig(HorizonConfiguration config, IFileSystem rootFileSystem,
            KeySet keySet)
        {
            var horizon = new Horizon(config);

            HorizonClient fsServerClient = horizon.CreatePrivilegedHorizonClient();
            var fsServer = new FileSystemServer(fsServerClient);

            var defaultObjects = DefaultFsServerObjects.GetDefaultEmulatedCreators(rootFileSystem, keySet, fsServer);

            var fsServerConfig = new FileSystemServerConfig
            {
                DeviceOperator = defaultObjects.DeviceOperator,
                ExternalKeySet = keySet.ExternalKeySet,
                FsCreators = defaultObjects.FsCreators,
            };

            FileSystemServerInitializer.InitializeWithConfig(fsServerClient, fsServer, fsServerConfig);

            HorizonClient bcatServerClient = horizon.CreateHorizonClient();
            _ = new BcatServer(bcatServerClient);

            return horizon;
        }
    }
}
