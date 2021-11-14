using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for mounting custom storage file systems.
/// </summary>
/// <remarks>Based on FS 12.1.0 (nnSdk 12.3.1)</remarks>
[SkipLocalsInit]
public static class CustomStorage
{
    public static ReadOnlySpan<byte> GetCustomStorageDirectoryName(CustomStorageId storageId)
    {
        switch (storageId)
        {
            case CustomStorageId.System:
            case CustomStorageId.SdCard:
                return CustomStorageDirectoryName;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    public static Result MountCustomStorage(this FileSystemClient fs, U8Span mountName, CustomStorageId storageId)
    {
        Result rc = Mount(fs, mountName, storageId);

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;

        static Result Mount(FileSystemClient fs, U8Span mountName, CustomStorageId storageId)
        {
            Result rc = fs.Impl.CheckMountName(mountName);
            if (rc.IsFailure()) return rc.Miss();

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var fileSystem = new SharedRef<IFileSystemSf>();

            rc = fileSystemProxy.Get.OpenCustomStorageFileSystem(ref fileSystem.Ref(), storageId);
            if (rc.IsFailure()) return rc.Miss();

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

            rc = fs.Register(mountName, ref fileSystemAdapter.Ref());
            if (rc.IsFailure()) return rc.Miss();

            return Result.Success;
        }
    }

    private static ReadOnlySpan<byte> CustomStorageDirectoryName => // "CustomStorage0"
        new[]
        {
            (byte)'C', (byte)'u', (byte)'s', (byte)'t', (byte)'o', (byte)'m', (byte)'S', (byte)'t',
            (byte)'o', (byte)'r', (byte)'a', (byte)'g', (byte)'e', (byte)'0'
        };
}
