using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    [SkipLocalsInit]
    public static class CustomStorage
    {
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

        public static Result MountCustomStorage(this FileSystemClient fs, U8Span mountName, CustomStorageId storageId)
        {
            Result rc = fs.Impl.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            ReferenceCountedDisposable<IFileSystemSf> fileSystem = null;
            try
            {
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                rc = fsProxy.Target.OpenCustomStorageFileSystem(out fileSystem, storageId);
                if (rc.IsFailure()) return rc;

                var fileSystemAdapter = new FileSystemServiceObjectAdapter(fileSystem);
                return fs.Register(mountName, fileSystemAdapter);
            }
            finally
            {
                fileSystem?.Dispose();
            }
        }

        private static ReadOnlySpan<byte> CustomStorageDirectoryName => // "CustomStorage0"
            new[]
            {
                (byte)'C', (byte)'u', (byte)'s', (byte)'t', (byte)'o', (byte)'m', (byte)'S', (byte)'t',
                (byte)'o', (byte)'r', (byte)'a', (byte)'g', (byte)'e', (byte)'0'
            };
    }
}
