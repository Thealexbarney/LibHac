using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Os;
using LibHac.Sdmmc;
using LibHac.Sf;
using static LibHac.Sdmmc.SdmmcApi;
using static LibHac.SdmmcSrv.SdmmcResultConverter;

namespace LibHac.SdmmcSrv;

/// <summary>
/// Performs various operations on the inserted SD card.
/// All available operations are listed in <see cref="SdCardOperationIdValue"/>.
/// </summary>
/// <remarks>Based on nnSdk 15.3.0 (FS 15.0.0)</remarks>
internal class SdCardDeviceOperator : IStorageDeviceOperator
{
    private SharedRef<SdCardStorageDevice> _storageDevice;

    // LibHac additions
    private readonly SdmmcApi _sdmmc;

    public SdCardDeviceOperator(ref SharedRef<SdCardStorageDevice> storageDevice, SdmmcApi sdmmc)
    {
        _storageDevice = SharedRef<SdCardStorageDevice>.CreateMove(ref storageDevice);
        _sdmmc = sdmmc;
    }

    public void Dispose()
    {
        _storageDevice.Destroy();
    }

    public Result Operate(int operationId)
    {
        return ResultFs.NotImplemented.Log();
    }

    public Result OperateIn(InBuffer buffer, long offset, long size, int operationId)
    {
        return ResultFs.NotImplemented.Log();
    }

    public Result OperateOut(out long bytesWritten, OutBuffer buffer, int operationId)
    {
        bytesWritten = 0;
        var operation = (SdCardOperationIdValue)operationId;

        using var scopedLock = new UniqueLockRef<SdkMutexType>();
        Result res = _storageDevice.Get.Lock(ref scopedLock.Ref());
        if (res.IsFailure()) return res.Miss();

        Port port = _storageDevice.Get.GetPort();

        switch (operation)
        {
            case SdCardOperationIdValue.GetSpeedMode:
            {
                if (buffer.Size < sizeof(SpeedMode))
                    return ResultFs.InvalidArgument.Log();

                res = GetFsResult(port, _sdmmc.GetDeviceSpeedMode(out buffer.As<SpeedMode>(), port));
                if (res.IsFailure()) return res.Miss();

                bytesWritten = sizeof(SpeedMode);
                return Result.Success;
            }
            case SdCardOperationIdValue.GetCid:
            {
                if (buffer.Size < DeviceCidSize)
                    return ResultFs.InvalidSize.Log();

                res = GetFsResult(port, _sdmmc.GetDeviceCid(buffer.Buffer.Slice(0, DeviceCidSize), port));
                if (res.IsFailure()) return res.Miss();

                bytesWritten = DeviceCidSize;
                return Result.Success;
            }
            case SdCardOperationIdValue.GetUserAreaNumSectors:
            {
                if (buffer.Size < sizeof(uint))
                    return ResultFs.InvalidArgument.Log();

                res = GetFsResult(port, _sdmmc.GetDeviceMemoryCapacity(out buffer.As<uint>(), port));
                if (res.IsFailure()) return res.Miss();

                bytesWritten = sizeof(uint);
                return Result.Success;
            }
            case SdCardOperationIdValue.GetUserAreaSize:
            {
                if (buffer.Size < sizeof(long))
                    return ResultFs.InvalidArgument.Log();

                res = GetFsResult(port, _sdmmc.GetDeviceMemoryCapacity(out uint numSectors, port));
                if (res.IsFailure()) return res.Miss();

                buffer.As<long>() = numSectors * SectorSize;
                bytesWritten = sizeof(long);

                return Result.Success;
            }
            case SdCardOperationIdValue.GetProtectedAreaNumSectors:
            {
                if (buffer.Size < sizeof(uint))
                    return ResultFs.InvalidArgument.Log();

                res = GetFsResult(port, _sdmmc.GetSdCardProtectedAreaCapacity(out buffer.As<uint>(), port));
                if (res.IsFailure()) return res.Miss();

                bytesWritten = sizeof(uint);
                return Result.Success;
            }
            case SdCardOperationIdValue.GetProtectedAreaSize:
            {
                if (buffer.Size < sizeof(long))
                    return ResultFs.InvalidArgument.Log();

                res = GetFsResult(port, _sdmmc.GetSdCardProtectedAreaCapacity(out uint numSectors, port));
                if (res.IsFailure()) return res.Miss();

                buffer.As<long>() = numSectors * SectorSize;
                bytesWritten = sizeof(long);

                return Result.Success;
            }
            default:
                return ResultFs.InvalidArgument.Log();
        }
    }

    public Result OperateOut2(out long bytesWrittenBuffer1, OutBuffer buffer1, out long bytesWrittenBuffer2,
        OutBuffer buffer2, int operationId)
    {
        UnsafeHelpers.SkipParamInit(out bytesWrittenBuffer1, out bytesWrittenBuffer2);

        return ResultFs.NotImplemented.Log();
    }

    public Result OperateInOut(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer, long offset, long size,
        int operationId)
    {
        UnsafeHelpers.SkipParamInit(out bytesWritten);

        return ResultFs.NotImplemented.Log();
    }

    public Result OperateIn2Out(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer1, InBuffer inBuffer2,
        long offset, long size, int operationId)
    {
        UnsafeHelpers.SkipParamInit(out bytesWritten);

        return ResultFs.NotImplemented.Log();
    }
}