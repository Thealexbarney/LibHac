using System;
using LibHac.Common;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Sf;

namespace LibHac.FsSrv.Storage
{
    public interface IStorageDeviceManagerFactory : IDisposable
    {
        Result Create(ref SharedRef<IStorageDeviceManager> outDeviceManager, StorageDevicePortId portId);
        Result SetReady(StorageDevicePortId portId, NativeHandle handle);
        Result UnsetReady(StorageDevicePortId portId);
    }
}