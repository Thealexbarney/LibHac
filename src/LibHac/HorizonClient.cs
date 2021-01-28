using System;
using LibHac.Arp;
using LibHac.Fs;
using LibHac.Lr;
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
        public OsState Os { get; }
        public LrClient Lr { get; }
        public ArpClient Arp => ArpLazy.Value;

        public ITimeSpanGenerator Time => Horizon.Time;

        internal HorizonClient(Horizon horizon, ProcessId processId)
        {
            Horizon = horizon;
            ProcessId = processId;

            Fs = new FileSystemClient(this);
            Sm = new ServiceManagerClient(horizon.ServiceManager);
            Os = new OsState(this, horizon.StartTick);
            Lr = new LrClient(this);

            ArpLazy = new Lazy<ArpClient>(InitArpClient, true);
        }

        private ArpClient InitArpClient()
        {
            return new ArpClient(this);
        }
    }
}
