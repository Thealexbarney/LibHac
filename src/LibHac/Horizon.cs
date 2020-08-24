using System.Threading;
using LibHac.Bcat;
using LibHac.Diag;
using LibHac.FsSrv;
using LibHac.Os;
using LibHac.Sm;

namespace LibHac
{
    public class Horizon
    {
        private const int InitialProcessCountMax = 0x50;
        internal ITimeSpanGenerator Time { get; }
        internal ServiceManager ServiceManager { get; }

        // long instead of ulong because the ulong Interlocked.Increment overload
        // wasn't added until .NET 5
        private long _currentInitialProcessId;
        private long _currentProcessId;

        public Horizon(ITimeSpanGenerator timer, FileSystemServerConfig fsServerConfig)
        {
            _currentProcessId = InitialProcessCountMax;

            Time = timer ?? new StopWatchTimeSpanGenerator();
            ServiceManager = new ServiceManager();

            // ReSharper disable ObjectCreationAsStatement
            new FileSystemServer(CreatePrivilegedHorizonClient(), fsServerConfig);
            new BcatServer(CreateHorizonClient());
            // ReSharper restore ObjectCreationAsStatement
        }

        public HorizonClient CreatePrivilegedHorizonClient()
        {
            ulong processId = (ulong)Interlocked.Increment(ref _currentInitialProcessId);

            Abort.DoAbortUnless(processId <= InitialProcessCountMax, "Created too many privileged clients.");

            // Todo: Register process with FS

            return new HorizonClient(this, new ProcessId(processId));
        }

        public HorizonClient CreateHorizonClient()
        {
            ulong processId = (ulong)Interlocked.Increment(ref _currentProcessId);

            // Todo: Register process with FS

            return new HorizonClient(this, new ProcessId(processId));
        }
    }
}
