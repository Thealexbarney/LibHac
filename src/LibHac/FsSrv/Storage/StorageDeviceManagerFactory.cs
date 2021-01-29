using LibHac.Diag;
using LibHac.FsSrv.Storage.Sf;

namespace LibHac.FsSrv.Storage
{
    internal static class StorageDeviceManagerFactory
    {
        public static Result CreateStorageDeviceManager(this StorageService storage,
            out ReferenceCountedDisposable<IStorageDeviceManager> deviceManager, StorageDevicePortId portId)
        {
            IStorageDeviceManagerFactory factory = storage.GetStorageDeviceManagerFactory();
            Assert.NotNull(factory);

            return factory.Create(out deviceManager, portId);
        }
    }
}
