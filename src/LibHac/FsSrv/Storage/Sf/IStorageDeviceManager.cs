using System;
using LibHac.Common;
using LibHac.FsSrv.Sf;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.FsSrv.Storage.Sf;

/// <summary>
/// Allows getting the current state of a storage device and opening various interfaces to operate on it.
/// </summary>
/// <remarks>Based on nnSdk 15.3.0 (FS 15.0.0)</remarks>
public interface IStorageDeviceManager : IDisposable
{
    Result IsInserted(out bool isInserted);
    Result IsHandleValid(out bool isValid, GameCardHandle handle);
    Result OpenDetectionEvent(ref SharedRef<IEventNotifier> outDetectionEvent);
    Result OpenOperator(ref SharedRef<IStorageDeviceOperator> outDeviceOperator);
    Result OpenDevice(ref SharedRef<IStorageDevice> outStorageDevice, ulong attribute);
    Result OpenStorage(ref SharedRef<IStorageSf> outStorage, ulong attribute);
    Result Invalidate();
}