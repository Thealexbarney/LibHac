using LibHac.Common;
using LibHac.Diag;
using LibHac.FsSrv.Storage.Sf;

namespace LibHac.FsSrv.Storage
{
    internal struct StorageDeviceManagerFactoryGlobals
    {
        public nint FactoryGuard;
        public IStorageDeviceManagerFactory Factory;
    }

    public static class StorageDeviceManagerFactoryApi
    {
        /// <summary>
        /// Sets the <see cref="IStorageDeviceManagerFactory"/> to be used by the <see cref="FileSystemServer"/>.
        /// Calling this method more than once will do nothing.
        /// </summary>
        /// <param name="storage">The Storage instance to use.</param>
        /// <param name="factory">The <see cref="IStorageDeviceManagerFactory"/> to be used by this Storage instance.</param>
        public static void InitializeStorageDeviceManagerFactory(this StorageService storage,
            IStorageDeviceManagerFactory factory)
        {
            storage.GetStorageDeviceManagerFactory(factory);
        }
    }

    internal static class StorageDeviceManagerFactory
    {
        public static Result CreateStorageDeviceManager(this StorageService storage,
            out ReferenceCountedDisposable<IStorageDeviceManager> deviceManager, StorageDevicePortId portId)
        {
            IStorageDeviceManagerFactory factory = storage.GetStorageDeviceManagerFactory(null);
            Assert.SdkNotNull(factory);

            return factory.Create(out deviceManager, portId);
        }

        public static IStorageDeviceManagerFactory GetStorageDeviceManagerFactory(this StorageService storage,
            IStorageDeviceManagerFactory factory)
        {
            ref StorageDeviceManagerFactoryGlobals g = ref storage.Globals.StorageDeviceManagerFactory;
            using var initGuard = new InitializationGuard(ref g.FactoryGuard, storage.Globals.InitMutex);

            if (initGuard.IsInitialized)
                return g.Factory;

            g.Factory = factory;
            return g.Factory;
        }
    }
}
