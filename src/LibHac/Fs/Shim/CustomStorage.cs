using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
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
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

                rc = fsProxy.Target.OpenCustomStorageFileSystem(out customFs, storageId);
                if (rc.IsFailure()) return rc;

                var adapter = new FileSystemServiceObjectAdapter(customFs);

                return fs.Register(mountName, adapter);
            }
            finally
            {
                customFs?.Dispose();
            }
        }

        public static U8Span GetCustomStorageDirectoryName(CustomStorageId storageId)
        {
            switch (storageId)
            {
                case CustomStorageId.System:
                case CustomStorageId.SdCard:
                    return new U8Span(CustomStorageDirectoryName);
                default:
                    Abort.UnexpectedDefault();
                    return default;
            }
        }

        private static ReadOnlySpan<byte> CustomStorageDirectoryName => // CustomStorage0
            new[]
            {
                (byte) 'C', (byte) 'u', (byte) 's', (byte) 't', (byte) 'o', (byte) 'm', (byte) 'S', (byte) 't',
                (byte) 'o', (byte) 'r', (byte) 'a', (byte) 'g', (byte) 'e', (byte) '0'
            };
    }
}
