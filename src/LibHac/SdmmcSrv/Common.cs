using LibHac.Sdmmc;

namespace LibHac.SdmmcSrv;

public static class Common
{
    public static Result CheckConnection(out SpeedMode outSpeedMode, out BusWidth outBusWidth, Port port)
    {
        outSpeedMode = default;
        outBusWidth = default;

        return Result.Success;
    }
}