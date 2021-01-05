using System;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataExporter : IDisposable
    {
        public Result GetSaveDataInfo(out SaveDataInfo info);
        public Result GetRestSize(out ulong remainingSize);
        public Result Pull(out ulong bytesRead, OutBuffer buffer);
        public Result PullInitialData(OutBuffer initialData);
    }
}