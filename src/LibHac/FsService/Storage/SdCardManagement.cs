namespace LibHac.FsService.Storage
{
    public static class SdCardManagement
    {
        public static Result GetCurrentSdCardHandle(out StorageDeviceHandle handle)
        {
            // todo: StorageDevice interfaces
            handle = new StorageDeviceHandle(1, StorageDevicePortId.SdCard);
            return Result.Success;
        }

        public static Result IsSdCardHandleValid(out bool isValid, in StorageDeviceHandle handle)
        {
            // todo: StorageDevice interfaces
            isValid = handle.PortId == StorageDevicePortId.SdCard;

            return Result.Success;
        }
    }
}
