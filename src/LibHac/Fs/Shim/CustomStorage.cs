using System;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.FsSrv;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    public static class CustomStorage
    {
        public static Result MountCustomStorage(this FileSystemClient fs, U8Span mountName, CustomStorageId storageId)
        {
            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            ReferenceCountedDisposable<IFileSystemSf> customFs = null;
            try
            {
                IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                rc = fsProxy.OpenCustomStorageFileSystem(out customFs, storageId);
                if (rc.IsFailure()) return rc;

                var adapter = new FileSystemServiceObjectAdapter(customFs);

                return fs.Register(mountName, adapter);
            }
            finally
            {
                customFs?.Dispose();
            }
        }

        public static string GetCustomStorageDirectoryName(CustomStorageId storageId)
        {
            switch (storageId)
            {
                case CustomStorageId.System:
                case CustomStorageId.SdCard:
                    return "CustomStorage0";
                default:
                    throw new ArgumentOutOfRangeException(nameof(storageId), storageId, null);
            }
        }
    }
}
