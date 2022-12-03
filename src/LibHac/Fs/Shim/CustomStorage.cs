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
/// <remarks>Based on nnSdk 14.3.0</remarks>
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
        Result res = Mount(fs, mountName, storageId);

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;

        static Result Mount(FileSystemClient fs, U8Span mountName, CustomStorageId storageId)
        {
            Result res = fs.Impl.CheckMountName(mountName);
            if (res.IsFailure()) return res.Miss();

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var fileSystem = new SharedRef<IFileSystemSf>();

            res = fileSystemProxy.Get.OpenCustomStorageFileSystem(ref fileSystem.Ref, storageId);
            if (res.IsFailure()) return res.Miss();

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref));

            res = fs.Register(mountName, ref fileSystemAdapter.Ref);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }

    /// <summary>"<c>CustomStorage0</c>"</summary>
    private static ReadOnlySpan<byte> CustomStorageDirectoryName => "CustomStorage0"u8;
}