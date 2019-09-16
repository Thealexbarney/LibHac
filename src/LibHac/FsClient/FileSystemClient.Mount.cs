using LibHac.Common;
using LibHac.Fs;
using LibHac.FsService;

namespace LibHac.FsClient
{
    public partial class FileSystemClient
    {
        public Result MountCustomStorage(U8Span mountName, CustomStorageId storageId)
        {
            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            FileSystemProxy fsProxy = GetFileSystemProxyServiceObject();

            rc = fsProxy.OpenCustomStorageFileSystem(out IFileSystem customFs, storageId);
            if (rc.IsFailure()) return rc;

            return FsManager.Register(mountName, customFs);
        }
    }
}
