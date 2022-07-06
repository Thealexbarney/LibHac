using LibHac.FsSystem;
using LibHac.Sdmmc;

namespace LibHac.SdmmcSrv;

internal class SdCardDetectionEventManager : CardDeviceDetectionEventManager
{
    // LibHac addition
    private readonly SdmmcApi _sdmmc;

    public SdCardDetectionEventManager(Port port, SdmmcApi sdmmc)
    {
        CallbackArgs.Port = port;
        _sdmmc = sdmmc;

        _sdmmc.RegisterSdCardDetectionEventCallback(port, DetectionEventCallback, CallbackArgs);
    }

    public override void Dispose()
    {
        _sdmmc.UnregisterSdCardDetectionEventCallback(CallbackArgs.Port);

        base.Dispose();
    }
}