using System;
using LibHac.Sf;

namespace LibHac.FsSrv.Storage.Sf;

/// <summary>
/// A generic interface for operating on a storage device or a storage device manager, containing methods that all take
/// an operation ID and various combinations of on offset/size, input buffers, and output buffers. 
/// </summary>
/// <remarks><para>Operation IDs are not common between implementers of the interface. Every implementer will have its own operations
/// and expected input data.</para>
/// <para>Based on nnSdk 16.2.0 (FS 16.0.0)</para></remarks>
public interface IStorageDeviceOperator : IDisposable
{
    Result Operate(int operationId);
    Result OperateIn(InBuffer buffer, long offset, long size, int operationId);
    Result OperateOut(out long bytesWritten, OutBuffer buffer, int operationId);
    Result OperateOut2(out long bytesWrittenBuffer1, OutBuffer buffer1, out long bytesWrittenBuffer2, OutBuffer buffer2, int operationId);
    Result OperateInOut(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer, long offset, long size, int operationId);
    Result OperateIn2Out(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer1, InBuffer inBuffer2, long offset, long size, int operationId);
}