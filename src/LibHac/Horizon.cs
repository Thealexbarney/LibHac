using System.Threading;
using LibHac.Bcat;
using LibHac.FsSrv;
using LibHac.Os;
using LibHac.Sm;

namespace LibHac
{
    public class Horizon
    {
        internal ITimeSpanGenerator Time { get; }
        internal ServiceManager ServiceManager { get; }

        // long instead of ulong because the ulong Interlocked.Increment overload
        // wasn't added until .NET 5
        private long _currentProcessId;

        public Horizon(ITimeSpanGenerator timer, FileSystemServerConfig fsServerConfig)
        {
            _currentProcessId = 0;

            Time = timer ?? new StopWatchTimeSpanGenerator();
            ServiceManager = new ServiceManager();

            // ReSharper disable ObjectCreationAsStatement
            new FileSystemServer(CreateHorizonClient(), fsServerConfig);
            new BcatServer(CreateHorizonClient());
            // ReSharper restore ObjectCreationAsStatement
        }

        public HorizonClient CreateHorizonClient()
        {
            ulong processId = (ulong)Interlocked.Increment(ref _currentProcessId);

            // Todo: Register process with FS

            return new HorizonClient(this, new ProcessId(processId));
        }
    }
}
