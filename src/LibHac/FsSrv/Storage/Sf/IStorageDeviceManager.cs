using System;
using LibHac.Common;
using LibHac.FsSrv.Sf;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.FsSrv.Storage.Sf;

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