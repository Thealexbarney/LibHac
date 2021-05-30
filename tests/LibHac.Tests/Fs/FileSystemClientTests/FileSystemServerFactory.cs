using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv;

namespace LibHac.Tests.Fs.FileSystemClientTests
{
    public static class FileSystemServerFactory
    {
        private static Horizon CreateHorizonImpl(bool sdCardInserted, out IFileSystem rootFs)
        {
            rootFs = new InMemoryFileSystem();
            var keySet = new KeySet();

            var horizon = new Horizon(new HorizonConfiguration());

            HorizonClient fsServerClient = horizon.CreatePrivilegedHorizonClient();
            var fsServer = new FileSystemServer(fsServerClient);

            var defaultObjects = DefaultFsServerObjects.GetDefaultEmulatedCreators(rootFs, keySet, fsServer);

            defaultObjects.SdCard.SetSdCardInsertionStatus(sdCardInserted);

            var config = new FileSystemServerConfig();
            config.FsCreators = defaultObjects.FsCreators;
            config.DeviceOperator = defaultObjects.DeviceOperator;
            config.ExternalKeySet = new ExternalKeySet();

            FileSystemServerInitializer.InitializeWithConfig(fsServerClient, fsServer, config);
            return horizon;
        }

        private static FileSystemClient CreateClientImpl(bool sdCardInserted, out IFileSystem rootFs)
        {
            Horizon horizon = CreateHorizonImpl(sdCardInserted, out rootFs);

            HorizonClient horizonClient = horizon.CreatePrivilegedHorizonClient();

            return horizonClient.Fs;
        }

        public static FileSystemClient CreateClient(bool sdCardInserted)
        {
            return CreateClientImpl(sdCardInserted, out _);
        }

        public static FileSystemClient CreateClient(out IFileSystem rootFs)
        {
            return CreateClientImpl(false, out rootFs);
        }

        public static Horizon CreateHorizonServer()
        {
            return CreateHorizonImpl(true, out _);
        }
    }
}
