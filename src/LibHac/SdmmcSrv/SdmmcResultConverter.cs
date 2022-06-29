using System;
using LibHac.Sdmmc;

namespace LibHac.SdmmcSrv;

public static class SdmmcResultConverter
{
    public static Result GetFsResult(Port port, Result result)
    {
        if (result.IsSuccess())
            return Result.Success;

        if (port == Port.Mmc0)
        {
            return GetFsResultFromMmcResult(result).Ret();
        }
        else
        {
            return GetFsResultFromSdCardResult(result).Ret();
        }
    }

    private static Result GetFsResultFromMmcResult(Result result)
    {
        throw new NotImplementedException();
    }

    private static Result GetFsResultFromSdCardResult(Result result)
    {
        throw new NotImplementedException();
    }
}