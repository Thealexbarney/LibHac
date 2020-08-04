using LibHac.FsService;
using LibHac.FsService.Storage;

namespace LibHac.Fs.Impl
{
    internal class SdHandleManager : IDeviceHandleManager
    {
        public Result GetHandle(out StorageDeviceHandle handle)
        {
            return SdCardManagement.GetCurrentSdCardHandle(out handle);
        }

        public bool IsValid(in StorageDeviceHandle handle)
        {
            // Note: Nintendo ignores the result here.
            SdCardManagement.IsSdCardHandleValid(out bool isValid, in handle).IgnoreResult();
            return isValid;
        }
    }
}
