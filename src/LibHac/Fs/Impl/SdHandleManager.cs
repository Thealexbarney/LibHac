using LibHac.FsSrv;
using LibHac.FsSrv.Storage;

namespace LibHac.Fs.Impl
{
    internal class SdHandleManager : IDeviceHandleManager
    {
        public Result GetHandle(out StorageDeviceHandle handle)
        {
            return GetCurrentSdCardHandle(out handle);
        }

        public bool IsValid(in StorageDeviceHandle handle)
        {
            // Note: Nintendo ignores the result here.
            IsSdCardHandleValid(out bool isValid, in handle).IgnoreResult();
            return isValid;
        }

        // Todo: Use FsSrv.Storage
        private static Result GetCurrentSdCardHandle(out StorageDeviceHandle handle)
        {
            handle = new StorageDeviceHandle(1, StorageDevicePortId.SdCard);
            return Result.Success;
        }

        private static Result IsSdCardHandleValid(out bool isValid, in StorageDeviceHandle handle)
        {
            isValid = handle.PortId == StorageDevicePortId.SdCard;

            return Result.Success;
        }
    }
}
