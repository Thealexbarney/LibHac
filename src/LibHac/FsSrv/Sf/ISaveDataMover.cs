using System;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataMover : IDisposable
    {
        Result Register(ulong saveDataId);
        Result Process(out long remainingSize, long sizeToProcess);
        Result Cancel();
    }
}