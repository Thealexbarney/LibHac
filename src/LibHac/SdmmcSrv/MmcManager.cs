using System;
using LibHac.Common;
using LibHac.FsSrv.Sf;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Os;
using LibHac.Sdmmc;
using LibHac.Sf;
using IStorage = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.SdmmcSrv;

public class MmcManager : IStorageDeviceManager, IStorageDeviceOperator, ISdmmcDeviceManager
{
    private Port _port;
    private bool _isInitialized;
    private bool _isActivated;
    private SdkMutexType _mutex;

    public MmcManager()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public void InitializeMmc()
    {
        throw new NotImplementedException();
    }

    public Result IsInserted(out bool isInserted)
    {
        throw new NotImplementedException();
    }

    public Result IsHandleValid(out bool isValid, SdmmcHandle handle)
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

    private Result OpenStorageDevice(ref SharedRef<IStorageDevice> outStorageDevice, ulong attribute)
    {
        throw new NotImplementedException();
    }

    public Result PutToSleep()
    {
        throw new NotImplementedException();
    }

    public Result Awaken()
    {
        throw new NotImplementedException();
    }

    public Result Shutdown()
    {
        throw new NotImplementedException();
    }

    public Result Invalidate()
    {
        throw new NotImplementedException();
    }

    public Result Lock(ref UniqueLockRef<SdkMutexType> outLock, SdmmcHandle handle)
    {
        throw new NotImplementedException();
    }

    public Fs.IStorage GetStorage()
    {
        throw new NotImplementedException();
    }

    public Port GetPort()
    {
        throw new NotImplementedException();
    }

    public void NotifyCloseStorageDevice(SdmmcHandle handle)
    {
        throw new NotImplementedException();
    }

    public Result Operate(int operationId)
    {
        throw new NotImplementedException();
    }

    public Result OperateIn(InBuffer buffer, long offset, long size, int operationId)
    {
        throw new NotImplementedException();
    }

    public Result OperateOut(out long bytesWritten, OutBuffer buffer, int operationId)
    {
        throw new NotImplementedException();
    }

    public Result OperateOut2(out long bytesWrittenBuffer1, OutBuffer buffer1, out long bytesWrittenBuffer2,
        OutBuffer buffer2, int operationId)
    {
        throw new NotImplementedException();
    }

    public Result OperateInOut(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer, long offset, long size,
        int operationId)
    {
        throw new NotImplementedException();
    }

    public Result OperateIn2Out(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer1, InBuffer inBuffer2,
        long offset, long size, int operationId)
    {
        throw new NotImplementedException();
    }
}