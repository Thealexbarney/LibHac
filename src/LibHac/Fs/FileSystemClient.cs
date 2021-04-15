using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSystem;

namespace LibHac.Fs
{
    public class FileSystemClient
    {
        internal FileSystemClientGlobals Globals;

        public FileSystemClientImpl Impl => new FileSystemClientImpl(this);
        internal HorizonClient Hos => Globals.Hos;

        public FileSystemClient(HorizonClient horizonClient)
        {
            Globals.Initialize(this, horizonClient);
        }
    }

    internal struct FileSystemClientGlobals
    {
        public HorizonClient Hos;
        public object InitMutex;
        public AccessLogGlobals AccessLog;
        public UserMountTableGlobals UserMountTable;
        public FileSystemProxyServiceObjectGlobals FileSystemProxyServiceObject;
        public FsContextHandlerGlobals FsContextHandler;
        public ResultHandlingUtilityGlobals ResultHandlingUtility;
        public PathUtilityGlobals PathUtility;
        public DirectorySaveDataFileSystemGlobals DirectorySaveDataFileSystem;

        public void Initialize(FileSystemClient fsClient, HorizonClient horizonClient)
        {
            Hos = horizonClient;
            InitMutex = new object();
            AccessLog.Initialize(fsClient);
            UserMountTable.Initialize(fsClient);
            FsContextHandler.Initialize(fsClient);
            PathUtility.Initialize(fsClient);
            DirectorySaveDataFileSystem.Initialize(fsClient);
        }
    }

    // Functions in the nn::fs::detail namespace use this struct.
    public readonly struct FileSystemClientImpl
    {
        internal readonly FileSystemClient Fs;
        internal HorizonClient Hos => Fs.Hos;
        internal ref FileSystemClientGlobals Globals => ref Fs.Globals;

        internal FileSystemClientImpl(FileSystemClient parentClient) => Fs = parentClient;
    }
}
