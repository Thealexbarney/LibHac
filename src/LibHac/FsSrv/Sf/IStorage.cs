using System;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface IStorage : IDisposable
    {
        Result Read(long offset, OutBuffer destination, long size);
        Result Write(long offset, InBuffer source, long size);
        Result Flush();
        Result SetSize(long size);
        Result GetSize(out long size);
        Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size);
    }
}