using System;
using LibHac.Common;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Sf;

namespace LibHac.FsSrv.Storage;

/// <summary>
/// Manages setting storage devices as ready or not ready, and allows opening <see cref="IStorageDeviceManager"/>s for
/// each storage device.
/// </summary>
/// <remarks>Based on nnSdk 15.3.0 (FS 15.0.0)</remarks>
public interface IStorageDeviceManagerFactory : IDisposable
{
    Result Create(ref SharedRef<IStorageDeviceManager> outDeviceManager, StorageDevicePortId portId);
    Result SetReady(StorageDevicePortId portId, NativeHandle handle);
    Result UnsetReady(StorageDevicePortId portId);
}