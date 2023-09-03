using LibHac.FsSystem;
using LibHac.Gc;

namespace LibHac.GcSrv;

/// <summary>
/// Manages registering events and signaling them when a game card is inserted or removed.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
internal class GameCardDetectionEventManager : CardDeviceDetectionEventManager
{
    private IGcApi _gc;

    public GameCardDetectionEventManager(IGcApi gc)
    {
        _gc = gc;

        gc.RegisterDetectionEventCallback(DetectionEventCallback, CallbackArgs);
    }

    public override void Dispose()
    {
        _gc.UnregisterDetectionEventCallback();

        base.Dispose();
    }
}