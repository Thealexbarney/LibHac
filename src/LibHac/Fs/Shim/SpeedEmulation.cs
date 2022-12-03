using LibHac.Common;
using LibHac.FsSrv.Sf;

namespace LibHac.Fs
{
    public enum SpeedEmulationMode
    {
        None = 0,
        Faster = 1,
        Slower = 2,
        Random = 3
    }
}

namespace LibHac.Fs.Shim
{
    public static class SpeedEmulationShim
    {
        public static Result SetSpeedEmulationMode(this FileSystemClient fs, SpeedEmulationMode mode)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var deviceOperator = new SharedRef<IDeviceOperator>();

            Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref);
            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            res = deviceOperator.Get.SetSpeedEmulationMode((int)mode);
            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public static Result GetSpeedEmulationMode(this FileSystemClient fs, out SpeedEmulationMode outMode)
        {
            UnsafeHelpers.SkipParamInit(out outMode);

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var deviceOperator = new SharedRef<IDeviceOperator>();

            Result res = fileSystemProxy.Get.OpenDeviceOperator(ref deviceOperator.Ref);
            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            res = deviceOperator.Get.GetSpeedEmulationMode(out int mode);
            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            outMode = (SpeedEmulationMode)mode;

            return Result.Success;
        }
    }
}