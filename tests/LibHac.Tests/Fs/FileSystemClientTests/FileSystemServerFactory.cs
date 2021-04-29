using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv;

namespace LibHac.Tests.Fs.FileSystemClientTests
{
    public static class FileSystemServerFactory
    {
        private static FileSystemClient CreateClientImpl(bool sdCardInserted, out IFileSystem rootFs)
        {
            rootFs = new InMemoryFileSystem();
            var keySet = new KeySet();
            
            var defaultObjects = DefaultFsServerObjects.GetDefaultEmulatedCreators(rootFs, keySet);

            defaultObjects.SdCard.SetSdCardInsertionStatus(sdCardInserted);

            var config = new FileSystemServerConfig();
            config.FsCreators = defaultObjects.FsCreators;
            config.DeviceOperator = defaultObjects.DeviceOperator;
            config.ExternalKeySet = new ExternalKeySet();
            config.KeySet = keySet;

            Horizon horizon = LibHac.HorizonFactory.CreateWithFsConfig(new HorizonConfiguration(), config);

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
    }
}
