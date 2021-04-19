using System;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface IFile : IDisposable
    {
        Result Read(out long bytesRead, long offset, OutBuffer destination, long size, ReadOption option);
        Result Write(long offset, InBuffer source, long size, WriteOption option);
        Result Flush();
        Result SetSize(long size);
        Result GetSize(out long size);
        Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size);
    }
}