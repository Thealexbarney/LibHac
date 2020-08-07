using LibHac.FsSrv.Storage;

namespace LibHac.FsSrv
{
    public interface IDeviceHandleManager
    {
        Result GetHandle(out StorageDeviceHandle handle);
        bool IsValid(in StorageDeviceHandle handle);
    }
}
