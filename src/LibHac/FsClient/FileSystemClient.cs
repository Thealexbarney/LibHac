using LibHac.FsService;

namespace LibHac.FsClient
{
    public class FileSystemClient
    {
        private FileSystemServer FsSrv { get; }
        private FileSystemProxy FsProxy { get; set; }
        private FileSystemManager FsManager { get; }

        private readonly object _fspInitLocker = new object();

        public FileSystemClient(FileSystemServer fsServer, ITimeSpanGenerator timer)
        {
            FsSrv = fsServer;
            FsManager = new FileSystemManager(timer);
        }

        private FileSystemProxy GetFileSystemProxyServiceObject()
        {
            if (FsProxy != null) return FsProxy;

            lock (_fspInitLocker)
            {
                if (FsProxy != null) return FsProxy;

                FsProxy = FsSrv.CreateFileSystemProxyService();

                return FsProxy;
            }
        }
    }
}
