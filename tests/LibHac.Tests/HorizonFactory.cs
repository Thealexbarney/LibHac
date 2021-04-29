using LibHac.Common.Keys;
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
            var keySet = new KeySet();

            var defaultObjects = DefaultFsServerObjects.GetDefaultEmulatedCreators(rootFs, keySet);

            var config = new FileSystemServerConfig();
            config.FsCreators = defaultObjects.FsCreators;
            config.DeviceOperator = defaultObjects.DeviceOperator;
            config.ExternalKeySet = new ExternalKeySet();
            config.KeySet = keySet;

            Horizon horizon = LibHac.HorizonFactory.CreateWithFsConfig(new HorizonConfiguration(), config);

            return horizon;
        }
    }
}
