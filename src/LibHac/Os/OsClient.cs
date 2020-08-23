namespace LibHac.Os
{
    public class OsClient
    {
        private HorizonClient Hos { get; }

        internal OsClient(HorizonClient horizonClient)
        {
            Hos = horizonClient;
        }

        public ProcessId GetCurrentProcessId()
        {
            return Hos.ProcessId;
        }
    }
}
