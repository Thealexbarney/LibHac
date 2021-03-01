using System;
using LibHac.Os.Impl;

namespace LibHac.Os
{
    public class OsState : IDisposable
    {
        private HorizonClient Hos { get; }
        internal OsResourceManager ResourceManager { get; }

        // Todo: Use configuration object if/when more options are added
        internal OsState(HorizonClient horizonClient, ITickGenerator tickGenerator)
        {
            Hos = horizonClient;
            ResourceManager = new OsResourceManager(tickGenerator);
        }

        public ProcessId GetCurrentProcessId()
        {
            return Hos.ProcessId;
        }

        public void Dispose()
        {
            ResourceManager.Dispose();
        }
    }
}
