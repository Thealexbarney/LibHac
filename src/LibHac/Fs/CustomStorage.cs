using System;
using LibHac.Common;
using LibHac.FsService;

namespace LibHac.Fs
{
    public static class CustomStorage
    {
        public static Result MountCustomStorage(this FileSystemClient fs, U8Span mountName, CustomStorageId storageId)
        {
            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            FileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            rc = fsProxy.OpenCustomStorageFileSystem(out IFileSystem customFs, storageId);
            if (rc.IsFailure()) return rc;

            return fs.Register(mountName, customFs);
        }

        public static string GetCustomStorageDirectoryName(CustomStorageId storageId)
        {
            switch (storageId)
            {
                case CustomStorageId.User:
                case CustomStorageId.SdCard:
                    return "CustomStorage0";
                default:
                    throw new ArgumentOutOfRangeException(nameof(storageId), storageId, null);
            }
        }
    }
}
