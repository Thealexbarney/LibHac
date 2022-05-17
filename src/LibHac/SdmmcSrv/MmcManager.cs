using System;
using LibHac.Common;
using LibHac.FsSrv.Sf;
using LibHac.FsSrv.Storage.Sf;

namespace LibHac.SdmmcSrv;

public class MmcManager : IStorageDeviceManager
{
    public MmcManager()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result IsInserted(out bool isInserted)
    {
        throw new NotImplementedException();
    }

    public Result IsHandleValid(out bool isValid, uint handle)
    {
        throw new NotImplementedException();
    }

    public Result OpenDetectionEvent(ref SharedRef<IEventNotifier> outDetectionEvent)
    {
        throw new NotImplementedException();
    }

    public Result OpenOperator(ref SharedRef<IStorageDeviceOperator> outDeviceOperator)
    {
        throw new NotImplementedException();
    }

    public Result OpenDevice(ref SharedRef<IStorageDevice> outStorageDevice, ulong attribute)
    {
        throw new NotImplementedException();
    }

    public Result OpenStorage(ref SharedRef<IStorage> outStorage, ulong attribute)
    {
        throw new NotImplementedException();
    }

    public Result Invalidate()
    {
        throw new NotImplementedException();
    }
}