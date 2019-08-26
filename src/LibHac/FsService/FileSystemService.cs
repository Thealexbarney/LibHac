using LibHac.FsService.Creators;

namespace LibHac.FsService
{
    public class FileSystemService
    {
        private FileSystemProxyCore FsProxyCore { get; }

        public FileSystemService(FileSystemCreators fsCreators)
        {
            FsProxyCore = new FileSystemProxyCore(fsCreators);
        }

        public FileSystemProxy CreateFileSystemProxyService()
        {
            return new FileSystemProxy(FsProxyCore);
        }
    }
}
