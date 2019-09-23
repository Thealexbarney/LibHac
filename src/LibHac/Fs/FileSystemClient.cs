using LibHac.Common;
using LibHac.FsService;

namespace LibHac.Fs
{
    public partial class FileSystemClient
    {
        private FileSystemServer FsSrv { get; }
        private IFileSystemProxy FsProxy { get; set; }
        private FileSystemManager FsManager { get; }

        private readonly object _fspInitLocker = new object();

        public FileSystemClient(FileSystemServer fsServer, ITimeSpanGenerator timer)
        {
            FsSrv = fsServer;
            FsManager = new FileSystemManager(timer);
        }

        public IFileSystemProxy GetFileSystemProxyServiceObject()
        {
            if (FsProxy != null) return FsProxy;

            lock (_fspInitLocker)
            {
                if (FsProxy != null) return FsProxy;

                FsProxy = FsSrv.CreateFileSystemProxyService();

                return FsProxy;
            }
        }

        public Result Register(U8Span mountName, IFileSystem fileSystem)
        {
            return FsManager.Register(mountName, fileSystem);
        }

        public Result Register(U8Span mountName, IFileSystem fileSystem, ICommonMountNameGenerator nameGenerator)
        {
            return FsManager.Register(mountName, fileSystem, nameGenerator);
        }
    }
}
