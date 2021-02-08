using System;
using LibHac.Os.Impl;

namespace LibHac.Os
{
    public class OsState : IDisposable
    {
        private HorizonClient Hos { get; }
        internal OsResourceManager ResourceManager { get; }

        // Todo: Use configuration object if/when more options are added
        internal OsState(HorizonClient horizonClient, long startTick)
        {
            Hos = horizonClient;
            ResourceManager = new OsResourceManager(startTick);
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
