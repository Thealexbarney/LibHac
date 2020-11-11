using System;
using LibHac.Fs;

namespace LibHac.FsSrv.Sf
{
    public interface IStorage : IDisposable
    {
        Result Read(long offset, Span<byte> destination);
        Result Write(long offset, ReadOnlySpan<byte> source);
        Result Flush();
        Result SetSize(long size);
        Result GetSize(out long size);
        Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size);
    }
}
