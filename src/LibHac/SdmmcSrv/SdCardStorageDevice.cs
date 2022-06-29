using System;
using LibHac.Common;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Os;
using LibHac.Sdmmc;
using LibHac.Sf;

namespace LibHac.SdmmcSrv;

internal class SdCardStorageDevice : SdmmcStorageInterfaceAdapter, IStorageDevice
{
    private SharedRef<ISdmmcDeviceManager> _manager;
    private SdmmcHandle _handle;

    public SdCardStorageDevice(ref SharedRef<ISdmmcDeviceManager> manager, SdmmcHandle handle)
        : base(manager.Get.GetStorage())
    {
        _manager = SharedRef<ISdmmcDeviceManager>.CreateMove(ref manager);
        _handle = handle;
    }

    public Port GetPort()
    {
        throw new NotImplementedException();
    }

    public Result GetHandle(out uint handle)
    {
        throw new NotImplementedException();
    }

    public Result IsHandleValid(out bool isValid)
    {
        throw new NotImplementedException();
    }

    public Result OpenOperator(ref SharedRef<IStorageDeviceOperator> outDeviceOperator)
    {
        throw new NotImplementedException();
    }

    public Result Lock(ref UniqueLockRef<SdkMutexType> outLock)
    {
        throw new NotImplementedException();
    }

    public override Result Read(long offset, OutBuffer destination, long size)
    {
        throw new NotImplementedException();
    }

    public override Result Write(long offset, InBuffer source, long size)
    {
        throw new NotImplementedException();
    }

    public override Result GetSize(out long size)
    {
        throw new NotImplementedException();
    }
}