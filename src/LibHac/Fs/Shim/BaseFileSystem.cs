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

        Result res = fileSystemProxy.Get.OpenBaseFileSystem(ref outFileSystem, fileSystemId);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private static Result RegisterFileSystem(FileSystemClient fs, U8Span mountName,
        ref readonly SharedRef<IFileSystemSf> fileSystem)
    {
        using var fileSystemAdapter =
            new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(in fileSystem));

        Result res = fs.Register(mountName, ref fileSystemAdapter.Ref);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result MountBaseFileSystem(this FileSystemClient fs, U8Span mountName, BaseFileSystemId fileSystemId)
    {
        Result res = fs.Impl.CheckMountName(mountName);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        using var fileSystem = new SharedRef<IFileSystemSf>();
        res = OpenBaseFileSystem(fs, ref fileSystem.Ref, fileSystemId);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = RegisterFileSystem(fs, mountName, in fileSystem);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result FormatBaseFileSystem(this FileSystemClient fs, BaseFileSystemId fileSystemId)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.FormatBaseFileSystem(fileSystemId);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}