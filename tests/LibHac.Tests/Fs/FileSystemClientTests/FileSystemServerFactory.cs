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

            var defaultObjects = DefaultFsServerObjects.GetDefaultEmulatedCreators(rootFs, new KeySet());

            defaultObjects.SdCard.SetSdCardInsertionStatus(sdCardInserted);

            var config = new FileSystemServerConfig();
            config.FsCreators = defaultObjects.FsCreators;
            config.DeviceOperator = defaultObjects.DeviceOperator;
            config.ExternalKeySet = new ExternalKeySet();

            var horizon = new Horizon(new StopWatchTimeSpanGenerator(), config);

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
