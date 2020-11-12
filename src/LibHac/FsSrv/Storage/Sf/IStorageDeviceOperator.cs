using System;

namespace LibHac.FsSrv.Storage.Sf
{
    public interface IStorageDeviceOperator : IDisposable
    {
        Result Operate(uint operationId);
        Result OperateIn(ReadOnlySpan<byte> buffer, long offset, long size, uint operationId);
        Result OperateOut(out long bytesWritten, Span<byte> buffer, long offset, long size, uint operationId);
        Result OperateOut2(out long bytesWrittenBuffer1, Span<byte> buffer1, out long bytesWrittenBuffer2, Span<byte> buffer2, uint operationId);
        Result OperateInOut(out long bytesWritten, Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, long offset, long size, uint operationId);
        Result OperateIn2Out(out long bytesWritten, Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer1, ReadOnlySpan<byte> inBuffer2, long offset, long size, uint operationId);
    }
}
