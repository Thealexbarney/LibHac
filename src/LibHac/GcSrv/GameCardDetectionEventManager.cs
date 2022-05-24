using LibHac.FsSystem;
using LibHac.Gc;

namespace LibHac.GcSrv;

/// <summary>
/// Manages registering events and signaling them when a game card is inserted or removed.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
internal class GameCardDetectionEventManager : CardDeviceDetectionEventManager
{
    private GameCardDummy _gc;

    public GameCardDetectionEventManager(GameCardDummy gc)
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