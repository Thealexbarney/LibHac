using LibHac.FsSystem;
using LibHac.Sdmmc;

namespace LibHac.SdmmcSrv;

/// <summary>
/// Registers an sdmmc detection callback when constructed, and unregisters the callback when disposed.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
internal class SdCardDetectionEventManager : CardDeviceDetectionEventManager
{
    public Port Port;

    // LibHac addition
    private readonly SdmmcApi _sdmmc;

    public SdCardDetectionEventManager(Port port, SdmmcApi sdmmc)
    {
        Port = port;
        _sdmmc = sdmmc;

        _sdmmc.RegisterSdCardDetectionEventCallback(port, DetectionEventCallback, CallbackArgs);
    }

    public override void Dispose()
    {
        _sdmmc.UnregisterSdCardDetectionEventCallback(Port);

        base.Dispose();
    }
}