using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv;

namespace LibHac.Tests
{
    public static class HorizonFactory
    {
        public static Horizon CreateBasicHorizon()
        {
            IFileSystem rootFs = new InMemoryFileSystem();

            var defaultObjects = DefaultFsServerObjects.GetDefaultEmulatedCreators(rootFs, new Keyset());

            var config = new FileSystemServerConfig();
            config.FsCreators = defaultObjects.FsCreators;
            config.DeviceOperator = defaultObjects.DeviceOperator;
            config.ExternalKeySet = new ExternalKeySet();

            var horizon = new Horizon(new StopWatchTimeSpanGenerator(), config);

            return horizon;
        }
    }
}
