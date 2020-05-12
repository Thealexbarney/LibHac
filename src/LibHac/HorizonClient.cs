using System;
using LibHac.Arp;
using LibHac.Fs;
using LibHac.Sm;

namespace LibHac
{
    public class HorizonClient
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private Horizon Horizon { get; }

        private Lazy<ArpClient> ArpLazy { get; }

        public FileSystemClient Fs { get; }
        public ServiceManagerClient Sm { get; }
        public ArpClient Arp => ArpLazy.Value;

        internal HorizonClient(Horizon horizon, FileSystemClient fsClient)
        {
            Horizon = horizon;

            Fs = fsClient;
            Sm = new ServiceManagerClient(horizon.ServiceManager);

            ArpLazy = new Lazy<ArpClient>(InitArpClient, true);
        }

        private ArpClient InitArpClient()
        {
            return new ArpClient(this);
        }
    }
}
