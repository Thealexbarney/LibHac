using System;

namespace LibHac.Lr
{
    public class LrClient : IDisposable
    {
        internal LrClientGlobals Globals;
        internal HorizonClient Hos => Globals.Hos;

        public LrClient(HorizonClient horizonClient)
        {
            Globals.Initialize(this, horizonClient);
        }

        public void Dispose()
        {
            Globals.Dispose();
        }
    }

    internal struct LrClientGlobals
    {
        public HorizonClient Hos;
        public LrServiceGlobals LrService;

        public void Initialize(LrClient lrClient, HorizonClient horizonClient)
        {
            Hos = horizonClient;
            LrService.Initialize();
        }

        public void Dispose()
        {
            LrService.Dispose();
        }
    }
}
