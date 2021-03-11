namespace LibHac.Diag
{
    public class DiagClient
    {
        internal DiagClientGlobals Globals;

        public DiagClientImpl Impl => new DiagClientImpl(this);
        internal HorizonClient Hos => Globals.Hos;

        public DiagClient(HorizonClient horizonClient)
        {
            Globals.Initialize(this, horizonClient);
        }
    }

    internal struct DiagClientGlobals
    {
        public HorizonClient Hos;
        public object InitMutex;
        public LogObserverGlobals LogObserver;

        public void Initialize(DiagClient diagClient, HorizonClient horizonClient)
        {
            Hos = horizonClient;
            InitMutex = new object();
        }
    }

    // Functions in the nn::diag::detail namespace use this struct.
    public readonly struct DiagClientImpl
    {
        internal readonly DiagClient Diag;
        internal HorizonClient Hos => Diag.Hos;
        internal ref DiagClientGlobals Globals => ref Diag.Globals;

        internal DiagClientImpl(DiagClient parentClient) => Diag = parentClient;
    }
}
