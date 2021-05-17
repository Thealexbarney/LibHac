using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Storage;

namespace LibHac.FsSrv
{
    public class FileSystemServer
    {
        internal FileSystemServerGlobals Globals;

        public FileSystemServerImpl Impl => new FileSystemServerImpl(this);
        public StorageService Storage => new StorageService(this);
        internal HorizonClient Hos => Globals.Hos;

        /// <summary>
        /// Creates a new <see cref="FileSystemServer"/> and registers its services using the provided HOS client.
        /// </summary>
        /// <param name="horizonClient">The <see cref="HorizonClient"/> that will be used by this server.</param>
        public FileSystemServer(HorizonClient horizonClient)
        {
            Globals.Initialize(horizonClient, this);
        }
    }

    internal struct FileSystemServerGlobals
    {
        public HorizonClient Hos;
        public object InitMutex;
        public FileSystemProxyImplGlobals FileSystemProxyImpl;
        public ProgramRegistryImplGlobals ProgramRegistryImpl;
        public DeviceEventSimulatorGlobals DeviceEventSimulator;
        public AccessControlGlobals AccessControl;
        public StorageDeviceManagerFactoryGlobals StorageDeviceManagerFactory;
        public SaveDataSharedFileStorageGlobals SaveDataSharedFileStorage;

        public void Initialize(HorizonClient horizonClient, FileSystemServer fsServer)
        {
            Hos = horizonClient;
            InitMutex = new object();

            SaveDataSharedFileStorage.Initialize(fsServer);
        }
    }

    // Functions in the nn::fssrv::storage namespace use this struct.
    public readonly struct StorageService
    {
        internal readonly FileSystemServer FsSrv;
        internal HorizonClient Hos => FsSrv.Hos;
        internal ref FileSystemServerGlobals Globals => ref FsSrv.Globals;

        internal StorageService(FileSystemServer parentServer) => FsSrv = parentServer;
    }

    // Functions in the nn::fssrv::detail namespace use this struct.
    public readonly struct FileSystemServerImpl
    {
        internal readonly FileSystemServer FsSrv;
        internal HorizonClient Hos => FsSrv.Hos;
        internal ref FileSystemServerGlobals Globals => ref FsSrv.Globals;

        internal FileSystemServerImpl(FileSystemServer parentServer) => FsSrv = parentServer;
    }
}
