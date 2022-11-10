using LibHac.Common;
using LibHac.FsSrv.Sf;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for suspending, resuming, and checking sdmmc status.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class SdmmcControl
{
    public static Result GetSdmmcConnectionStatus(this FileSystemClient fs, out SdmmcSpeedMode outSpeedMode,
        out SdmmcBusWidth outBusWidth, SdmmcPort port)
    {
        UnsafeHelpers.SkipParamInit(out outSpeedMode, out outBusWidth);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.GetSdmmcConnectionStatus(out int speedMode, out int busWidth, (int)port);
        if (res.IsFailure()) return res.Miss();

        outSpeedMode = (SdmmcSpeedMode)speedMode;
        outBusWidth = (SdmmcBusWidth)busWidth;

        return Result.Success;
    }

    public static Result SuspendSdmmcControl(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.SuspendSdmmcControl();
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result ResumeSdmmcControl(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var deviceOperator = new SharedRef<IDeviceOperator>();

        Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref());
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = deviceOperator.Get.ResumeSdmmcControl();
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}