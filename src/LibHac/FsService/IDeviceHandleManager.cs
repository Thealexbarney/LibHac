using LibHac.FsService.Storage;

namespace LibHac.FsService
{
    public interface IDeviceHandleManager
    {
        Result GetHandle(out StorageDeviceHandle handle);
        bool IsValid(in StorageDeviceHandle handle);
    }
}
