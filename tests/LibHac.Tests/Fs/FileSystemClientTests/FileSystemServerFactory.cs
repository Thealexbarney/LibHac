using LibHac.Fs;
using LibHac.FsService;

namespace LibHac.Tests.Fs.FileSystemClientTests
{
    public static class FileSystemServerFactory
    {
        public static FileSystemServer CreateServer(bool sdCardInserted)
        {
            var rootFs = new InMemoryFileSystem();

            var defaultObjects = DefaultFsServerObjects.GetDefaultEmulatedCreators(rootFs, new Keyset());

            defaultObjects.SdCard.SetSdCardInsertionStatus(sdCardInserted);

            var config = new FileSystemServerConfig();
            config.FsCreators = defaultObjects.FsCreators;
            config.DeviceOperator = defaultObjects.DeviceOperator;
            config.ExternalKeySet = new ExternalKeySet();

            var fsServer = new FileSystemServer(config);

            return fsServer;
        }

        public static FileSystemClient CreateClient(bool sdCardInserted)
        {
            FileSystemServer fsServer = CreateServer(sdCardInserted);

            return fsServer.CreateFileSystemClient();
        }
    }
}
