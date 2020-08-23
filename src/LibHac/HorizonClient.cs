using System;
using LibHac.Arp;
using LibHac.Fs;
using LibHac.Os;
using LibHac.Sm;

namespace LibHac
{
    public class HorizonClient
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private Horizon Horizon { get; }
        internal ProcessId ProcessId { get; }

        private Lazy<ArpClient> ArpLazy { get; }

        public FileSystemClient Fs { get; }
        public ServiceManagerClient Sm { get; }
        public OsClient Os { get; }
        public ArpClient Arp => ArpLazy.Value;

        public ITimeSpanGenerator Time => Horizon.Time;

        internal HorizonClient(Horizon horizon, ProcessId processId)
        {
            Horizon = horizon;
            ProcessId = processId;

            Fs = new FileSystemClient(this);
            Sm = new ServiceManagerClient(horizon.ServiceManager);
            Os = new OsClient(this);

            ArpLazy = new Lazy<ArpClient>(InitArpClient, true);
        }

        private ArpClient InitArpClient()
        {
            return new ArpClient(this);
        }
    }
}
