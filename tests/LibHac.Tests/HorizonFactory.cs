using LibHac.Bcat;
using LibHac.Common.Keys;
using LibHac.Fs.Fsa;
using LibHac.FsSrv;
using LibHac.FsSystem;
using LibHac.Tools.Fs;

namespace LibHac.Tests;

public static class HorizonFactory
{
    public static Horizon CreateBasicHorizon()
    {
        IFileSystem rootFs = new InMemoryFileSystem();
        var keySet = new KeySet();

        var horizon = new Horizon(new HorizonConfiguration());

        HorizonClient fsServerClient = horizon.CreatePrivilegedHorizonClient();
        var fsServer = new FileSystemServer(fsServerClient);

        var random = new Random(12345);
        RandomDataGenerator randomGenerator = buffer => random.NextBytes(buffer);

        var defaultObjects = DefaultFsServerObjects.GetDefaultEmulatedCreators(rootFs, keySet, fsServer, randomGenerator);

        var config = new FileSystemServerConfig();
        config.FsCreators = defaultObjects.FsCreators;
        config.DeviceOperator = defaultObjects.DeviceOperator;
        config.ExternalKeySet = new ExternalKeySet();
        config.RandomGenerator = randomGenerator;

        FileSystemServerInitializer.InitializeWithConfig(fsServerClient, fsServer, config);

        HorizonClient bcatServerClient = horizon.CreateHorizonClient();
        _ = new BcatServer(bcatServerClient);

        return horizon;
    }
}