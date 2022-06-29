using System;
using LibHac.Common;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Sf;

namespace LibHac.SdmmcSrv;

internal class SdCardDeviceOperator : IStorageDeviceOperator
{
    private SharedRef<SdCardStorageDevice> _storageDevice;

    public SdCardDeviceOperator(ref SharedRef<SdCardStorageDevice> storageDevice)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        _storageDevice.Destroy();
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