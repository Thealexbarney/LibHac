using System;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataChunkImporter : IDisposable
    {
        public Result Push(InBuffer buffer, ulong size);
    }
}