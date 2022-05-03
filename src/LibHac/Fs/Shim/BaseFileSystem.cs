using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;

using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for mounting base file systems.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class BaseFileSystem
{
    private static Result OpenBaseFileSystem(FileSystemClient fs, ref SharedRef<IFileSystemSf> outFileSystem,
        BaseFileSystemId fileSystemId)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.OpenBaseFileSystem(ref outFileSystem, fileSystemId);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    private static Result RegisterFileSystem(FileSystemClient fs, U8Span mountName,
        ref SharedRef<IFileSystemSf> fileSystem)
    {
        using var fileSystemAdapter =
            new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

        Result rc = fs.Register(mountName, ref fileSystemAdapter.Ref());
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result MountBaseFileSystem(this FileSystemClient fs, U8Span mountName, BaseFileSystemId fileSystemId)
    {
        Result rc = fs.Impl.CheckMountName(mountName);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        using var fileSystem = new SharedRef<IFileSystemSf>();
        rc = OpenBaseFileSystem(fs, ref fileSystem.Ref(), fileSystemId);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = RegisterFileSystem(fs, mountName, ref fileSystem.Ref());
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result FormatBaseFileSystem(this FileSystemClient fs, BaseFileSystemId fileSystemId)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.FormatBaseFileSystem(fileSystemId);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }
}