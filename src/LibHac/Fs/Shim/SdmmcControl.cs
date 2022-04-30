using System;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for suspending, resuming, and checking sdmmc status.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class SdmmcControl
{
    public static Result GetSdmmcConnectionStatus(this FileSystemClient fs, out SdmmcSpeedMode speedMode,
        out SdmmcBusWidth busWidth, SdmmcPort port)
    {
        throw new NotImplementedException();
    }

    public static Result SuspendSdmmcControl()
    {
        throw new NotImplementedException();
    }

    public static Result ResumeSdmmcControl()
    {
        throw new NotImplementedException();
    }
}