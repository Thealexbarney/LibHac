using System;
using LibHac.Fs;

namespace LibHac.FsSrv.Storage.Sf
{
    public interface IStorageDevice : IDisposable
    {
        Result GetHandle(out uint handle);
        Result IsHandleValid(out bool isValid);
        Result OpenOperator(out ReferenceCountedDisposable<IStorageDeviceOperator> deviceOperator);
        Result Read(long offset, Span<byte> destination);
        Result Write(long offset, ReadOnlySpan<byte> source);
        Result Flush();
        Result SetSize(long size);
        Result GetSize(out long size);
        Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size);
    }
}
