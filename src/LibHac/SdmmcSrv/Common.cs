using System;
using LibHac.Fs;
using LibHac.Sdmmc;

namespace LibHac.SdmmcSrv;

internal static class Common
{
    public static Result CheckConnection(out SpeedMode outSpeedMode, out BusWidth outBusWidth, Port port)
    {
        outSpeedMode = default;
        outBusWidth = default;

        return Result.Success;
    }

    public static Result GetAndClearSdmmcStorageErrorInfo(out StorageErrorInfo outStorageErrorInfo, out long outLogSize,
        Span<byte> logBuffer, SdmmcApi sdmmc)
    {
        throw new NotImplementedException();
    }

    public static uint BytesToSectors(long byteCount)
    {
        return (uint)((ulong)byteCount / SdmmcApi.SectorSize);
    }
}