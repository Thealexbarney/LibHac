using LibHac.FsSrv;
using LibHac.FsSrv.Storage;

namespace LibHac.Fs.Impl;

/// <summary>
/// Allows getting the current handle for the SD card and checking to see if a provided handle is still valid.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
internal class SdHandleManager : IDeviceHandleManager
{
    // LibHac addition
    private readonly FileSystemServer _fsServer;

    public SdHandleManager(FileSystemServer fsServer)
    {
        _fsServer = fsServer;
    }

    public Result GetHandle(out StorageDeviceHandle handle)
    {
        return _fsServer.Storage.GetCurrentSdCardHandle(out handle).Ret();
    }

    public bool IsValid(in StorageDeviceHandle handle)
    {
        // Note: Nintendo ignores the result here.
        _fsServer.Storage.IsSdCardHandleValid(out bool isValid, in handle).IgnoreResult();
        return isValid;
    }
}