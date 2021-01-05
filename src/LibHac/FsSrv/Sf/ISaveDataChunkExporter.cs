using System;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataChunkExporter : IDisposable
    {
        public Result Pull(out ulong bytesRead, OutBuffer buffer, ulong size);
        public Result GetRestRawDataSize(out long remainingSize);
    }
}