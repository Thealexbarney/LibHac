using System;
using LibHac.Common;
using LibHac.FsSrv.Sf;
using LibHac.Sf;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions used for interacting with the MMC.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class Mmc
{
    public static Result GetMmcSpeedMode(this FileSystemClient fs, out MmcSpeedMode outSpeedMode)
    {
        UnsafeHelpers.SkipParamInit(out outSpeedMode);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetMmcSpeedMode(out long speedMode);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        outSpeedMode = (MmcSpeedMode)speedMode;
        return Result.Success;
    }

    public static Result GetMmcCid(this FileSystemClient fs, Span<byte> outCidBuffer)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetMmcCid(new OutBuffer(outCidBuffer), outCidBuffer.Length);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result EraseMmc(this FileSystemClient fs, MmcPartition partition)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.EraseMmc((uint)partition);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result GetMmcPartitionSize(this FileSystemClient fs, out long outPartitionSize,
        MmcPartition partition)
    {
        UnsafeHelpers.SkipParamInit(out outPartitionSize);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetMmcPartitionSize(out outPartitionSize, (uint)partition);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result GetMmcPatrolCount(this FileSystemClient fs, out uint outPatrolCount)
    {
        UnsafeHelpers.SkipParamInit(out outPatrolCount);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetMmcPatrolCount(out outPatrolCount);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result GetAndClearMmcErrorInfo(this FileSystemClient fs, out StorageErrorInfo outErrorInfo,
        out long outLogSize, Span<byte> logBuffer)
    {
        UnsafeHelpers.SkipParamInit(out outErrorInfo, out outLogSize);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetAndClearMmcErrorInfo(out outErrorInfo, out long logSize, new OutBuffer(logBuffer),
            logBuffer.Length);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        outLogSize = logSize;

        return Result.Success;
    }

    public static Result GetMmcExtendedCsd(this FileSystemClient fs, Span<byte> outBuffer)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetMmcExtendedCsd(new OutBuffer(outBuffer), outBuffer.Length);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result SuspendMmcPatrol(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.SuspendMmcPatrol();
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result ResumeMmcPatrol(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.ResumeMmcPatrol();
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}