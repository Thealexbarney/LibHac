using System;

namespace LibHac.FsSrv.Sf
{
    public interface ISaveDataTransferProhibiter : IDisposable
    {
        // No methods. Disposing the service object removes the prohibition.
    }
}