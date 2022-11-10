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
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class DebugShim
{
    public static Result CreatePaddingFile(this FileSystemClient fs, long size)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.CreatePaddingFile(size);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result DeleteAllPaddingFiles(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.DeleteAllPaddingFiles();
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result OverrideSaveDataTransferTokenSignVerificationKey(this FileSystemClient fs,
        ReadOnlySpan<byte> keyBuffer)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.OverrideSaveDataTransferTokenSignVerificationKey(new InBuffer(keyBuffer));
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result SetDebugOption(this FileSystemClient fs, DebugOptionKey key, long value)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.RegisterDebugConfiguration((uint)key, value);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result UnsetDebugOption(this FileSystemClient fs, DebugOptionKey key)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.UnregisterDebugConfiguration((uint)key);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}