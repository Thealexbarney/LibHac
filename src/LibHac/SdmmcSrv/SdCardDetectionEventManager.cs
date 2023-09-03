using LibHac.FsSystem;
using LibHac.Sdmmc;

namespace LibHac.SdmmcSrv;

/// <summary>
/// Registers an sdmmc detection callback when constructed, and unregisters the callback when disposed.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
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