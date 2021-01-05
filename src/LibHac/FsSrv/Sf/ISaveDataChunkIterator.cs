using System;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataChunkIterator : IDisposable
    {
        public Result Next();
        public Result IsEnd(out bool isEnd);
        public Result GetId(out uint chunkId);
    }
}