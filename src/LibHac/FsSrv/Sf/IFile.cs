using System;
using LibHac.Fs;

namespace LibHac.FsSrv.Sf
{
    public interface IFile : IDisposable
    {
        Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption option);
        Result Write(long offset, ReadOnlySpan<byte> source, WriteOption option);
        Result Flush();
        Result SetSize(long size);
        Result GetSize(out long size);
        Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size);
    }
}
