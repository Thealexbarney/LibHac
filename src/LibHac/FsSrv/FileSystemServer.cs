using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Storage;

namespace LibHac.FsSrv
{
    public class FileSystemServer
    {
        internal FileSystemServerGlobals Globals;

        public FileSystemServerImpl Impl => new FileSystemServerImpl(this);
        public StorageService Storage => new StorageService(this);

        /// <summary>
        /// Creates a new <see cref="FileSystemServer"/> and registers its services using the provided HOS client.
        /// </summary>
        /// <param name="horizonClient">The <see cref="HorizonClient"/> that will be used by this server.</param>
        public FileSystemServer(HorizonClient horizonClient)
        {
            Globals.Hos = horizonClient;
            Globals.InitMutex = new object();
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
    }

    // Functions in the nn::fssrv::storage namespace use this struct.
    public readonly struct StorageService
    {
        internal readonly FileSystemServer FsSrv;

        internal StorageService(FileSystemServer parentServer) => FsSrv = parentServer;
    }

    // Functions in the nn::fssrv::detail namespace use this struct.
    public readonly struct FileSystemServerImpl
    {
        internal readonly FileSystemServer FsSrv;

        public FileSystemServerImpl(FileSystemServer parentServer) => FsSrv = parentServer;
    }
}
