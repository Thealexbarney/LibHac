using System;
using LibHac.FsSrv.Sf;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.FsSrv.Storage.Sf
{
    public interface IStorageDeviceManager : IDisposable
    {
        Result IsInserted(out bool isInserted);
        Result IsHandleValid(out bool isValid, uint handle);
        Result OpenDetectionEvent(out ReferenceCountedDisposable<IEventNotifier> eventNotifier);
        Result OpenOperator(out ReferenceCountedDisposable<IStorageDeviceOperator> deviceOperator);
        Result OpenDevice(out ReferenceCountedDisposable<IStorageDevice> storageDevice, ulong attribute);
        Result OpenStorage(out ReferenceCountedDisposable<IStorageSf> storage, ulong attribute);
        Result PutToSleep();
        Result Awaken();
        Result Initialize();
        Result Shutdown();
        Result Invalidate();
    }
}
