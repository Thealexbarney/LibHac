using System;
using LibHac.Sf;

namespace LibHac.FsSrv.Storage.Sf
{
    public interface IStorageDeviceOperator : IDisposable
    {
        Result Operate(int operationId);
        Result OperateIn(InBuffer buffer, long offset, long size, int operationId);
        Result OperateOut(out long bytesWritten, OutBuffer buffer, int operationId);
        Result OperateOut2(out long bytesWrittenBuffer1, OutBuffer buffer1, out long bytesWrittenBuffer2, OutBuffer buffer2, int operationId);
        Result OperateInOut(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer, long offset, long size, int operationId);
        Result OperateIn2Out(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer1, InBuffer inBuffer2, long offset, long size, int operationId);
    }
}
