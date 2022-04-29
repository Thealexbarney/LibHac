using System;
using LibHac.Common;
using LibHac.FsSrv.Sf;
using LibHac.Sf;

namespace LibHac.Fs.Shim;

public enum DebugOptionKey : uint
{
    SaveDataEncryption = 0x20454453, // "SDE "
    SaveDataJournalMetaVerification = 0x20454453, // "JMV "
    SaveDataRemapMetaVerification = 0x20454453, // "RMV "
    SaveDataHashAlgorithm = 0x20454453, // "SDHA"
    SaveDataHashSalt = 0x20454453 // "SDHS"
}

/// <summary>
/// Contains debug-related functions for the FS service.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0</remarks>
public static class DebugShim
{
    public static Result CreatePaddingFile(this FileSystemClient fs, long size)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.CreatePaddingFile(size);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result DeleteAllPaddingFiles(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.DeleteAllPaddingFiles();
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result OverrideSaveDataTransferTokenSignVerificationKey(this FileSystemClient fs,
        ReadOnlySpan<byte> keyBuffer)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.OverrideSaveDataTransferTokenSignVerificationKey(new InBuffer(keyBuffer));
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result SetDebugOption(this FileSystemClient fs, DebugOptionKey key, long value)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.RegisterDebugConfiguration((uint)key, value);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result UnsetDebugOption(this FileSystemClient fs, DebugOptionKey key)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.UnregisterDebugConfiguration((uint)key);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }
}