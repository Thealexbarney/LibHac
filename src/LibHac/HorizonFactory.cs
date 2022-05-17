using System;
using LibHac.Bcat;
using LibHac.Common.Keys;
using LibHac.Fs.Fsa;
using LibHac.FsSrv;
using LibHac.FsSystem;

namespace LibHac;

public static class HorizonFactory
{
    public static Horizon CreateWithDefaultFsConfig(HorizonConfiguration config, IFileSystem rootFileSystem,
        KeySet keySet)
    {
        var horizon = new Horizon(config);

        HorizonClient fsServerClient = horizon.CreatePrivilegedHorizonClient();
        var fsServer = new FileSystemServer(fsServerClient);

        var random = new Random();
        RandomDataGenerator randomGenerator = buffer => random.NextBytes(buffer);

        var defaultObjects = DefaultFsServerObjects.GetDefaultEmulatedCreators(rootFileSystem, keySet, fsServer, randomGenerator);

        var fsServerConfig = new FileSystemServerConfig
        {
            ExternalKeySet = keySet.ExternalKeySet,
            FsCreators = defaultObjects.FsCreators,
            StorageDeviceManagerFactory = defaultObjects.StorageDeviceManagerFactory,
            RandomGenerator = randomGenerator
        };

        FileSystemServerInitializer.InitializeWithConfig(fsServerClient, fsServer, fsServerConfig);

        HorizonClient bcatServerClient = horizon.CreateHorizonClient();
        _ = new BcatServer(bcatServerClient);

        return horizon;
    }
}