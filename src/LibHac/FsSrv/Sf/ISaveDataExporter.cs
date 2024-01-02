using System;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf;

public interface ISaveDataExporter : IDisposable
{
    public Result GetSaveDataInfo(out SaveDataInfo outInfo);
    public Result GetRestSize(out ulong outRemainingSize);
    public Result Pull(out ulong outBytesRead, OutBuffer buffer);
    public Result PullInitialData(OutBuffer initialDataBuffer);
}