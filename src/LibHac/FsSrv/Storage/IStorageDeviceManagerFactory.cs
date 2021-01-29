using System;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Sf;

namespace LibHac.FsSrv.Storage
{
    public interface IStorageDeviceManagerFactory : IDisposable
    {
        Result Create(out ReferenceCountedDisposable<IStorageDeviceManager> deviceManager, StorageDevicePortId portId);
        Result SetReady(StorageDevicePortId portId, NativeHandle handle);
        Result UnsetReady(StorageDevicePortId portId);
    }
}