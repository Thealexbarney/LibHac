using System.Threading;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Impl;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Sm;

namespace LibHac
{
    public class Horizon
    {
        private const int InitialProcessCountMax = 0x50;

        internal ITickGenerator TickGenerator { get; }
        internal ServiceManager ServiceManager { get; }
        private HorizonClient LoaderClient { get; }

        private ulong _currentInitialProcessId;
        private ulong _currentProcessId;

        public Horizon(HorizonConfiguration config)
        {
            _currentProcessId = InitialProcessCountMax;

            TickGenerator = config.TickGenerator ?? new DefaultTickGenerator();
            ServiceManager = new ServiceManager();

            LoaderClient = CreatePrivilegedHorizonClient();
        }

        public HorizonClient CreatePrivilegedHorizonClient()
        {
            ulong processId = Interlocked.Increment(ref _currentInitialProcessId);

            Abort.DoAbortUnless(processId <= InitialProcessCountMax, "Created too many privileged clients.");

            // Todo: Register process with FS

            return new HorizonClient(this, new ProcessId(processId));
        }

        public HorizonClient CreateHorizonClient()
        {
            ulong processId = Interlocked.Increment(ref _currentProcessId);

            // Todo: Register process with FS

            return new HorizonClient(this, new ProcessId(processId));
        }

        public HorizonClient CreateHorizonClient(ProgramId programId, AccessControlBits.Bits fsPermissions)
        {
            HorizonClient client = CreateHorizonClient();

            var dataHeader = new AccessControlDataHeader();
            var descriptor = new AccessControlDescriptor();

            descriptor.Version = 1;
            dataHeader.Version = 1;

            descriptor.AccessFlags = (ulong)fsPermissions;
            dataHeader.AccessFlags = (ulong)fsPermissions;

            LoaderClient.Fs.RegisterProgram(client.ProcessId.Value, programId, StorageId.BuiltInUser,
                    SpanHelpers.AsReadOnlyByteSpan(in dataHeader), SpanHelpers.AsReadOnlyByteSpan(in descriptor))
                .ThrowIfFailure();

            return client;
        }
    }
}
