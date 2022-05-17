using System;
using LibHac.FsSystem;

namespace LibHac.SdmmcSrv;

internal class SdCardDeviceDetectionEventManager : CardDeviceDetectionEventManager
{
    public SdCardDeviceDetectionEventManager(Sdmmc.Port port)
    {
        CallbackArgs.Port = port;

        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }
}