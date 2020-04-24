using LibHac.Arp.Impl;
using LibHac.Ns;

namespace LibHac.Arp
{
    public class ArpClient
    {
        private HorizonClient HosClient { get; }
        private IReader Reader { get; set; }

        private readonly object _readerInitLocker = new object();

        internal ArpClient(HorizonClient horizonClient)
        {
            HosClient = horizonClient;
        }

        public Result GetApplicationLaunchProperty(out ApplicationLaunchProperty launchProperty, ulong processId)
        {
            EnsureReaderInitialized();

            return Reader.GetApplicationLaunchProperty(out launchProperty, processId);
        }

        public Result GetApplicationLaunchProperty(out ApplicationLaunchProperty launchProperty, ApplicationId applicationId)
        {
            EnsureReaderInitialized();

            return Reader.GetApplicationLaunchPropertyWithApplicationId(out launchProperty, applicationId);
        }

        public Result GetApplicationControlProperty(out ApplicationControlProperty controlProperty, ulong processId)
        {
            EnsureReaderInitialized();

            return Reader.GetApplicationControlProperty(out controlProperty, processId);
        }

        public Result GetApplicationControlProperty(out ApplicationControlProperty controlProperty, ApplicationId applicationId)
        {
            EnsureReaderInitialized();

            return Reader.GetApplicationControlPropertyWithApplicationId(out controlProperty, applicationId);
        }

        private void EnsureReaderInitialized()
        {
            if (Reader != null)
                return;

            lock (_readerInitLocker)
            {
                if (Reader != null)
                    return;

                Result rc = HosClient.Sm.GetService(out IReader reader, "arp:r");

                if (rc.IsFailure())
                {
                    throw new HorizonResultException(rc, "Failed to initialize arp reader.");
                }

                Reader = reader;
            }
        }
    }
}
