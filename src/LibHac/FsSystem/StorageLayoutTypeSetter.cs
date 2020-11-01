using System;

namespace LibHac.FsSystem
{
    internal struct ScopedStorageLayoutTypeSetter : IDisposable
    {
        // ReSharper disable once UnusedParameter.Local
        public ScopedStorageLayoutTypeSetter(StorageType storageFlag)
        {
            // Todo: Implement
        }

        public void Dispose()
        {

        }
    }

    [Flags]
    internal enum StorageType
    {
        Bis = 1 << 0,
        SdCard = 1 << 1,
        GameCard = 1 << 2,
        Usb = 1 << 3,

        NonGameCard = Bis | SdCard | Usb,
        All = Bis | SdCard | GameCard | Usb
    }
}
