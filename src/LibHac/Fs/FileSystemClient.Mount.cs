using LibHac.Common;
using LibHac.FsSystem;
using LibHac.FsService;

namespace LibHac.Fs
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
