using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Accessors;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;

namespace LibHac.Fs
{
    // Functions in the nn::fssrv::detail namespace use this struct.
    public readonly struct FileSystemClientImpl
    {
        internal readonly FileSystemClient Fs;
        internal HorizonClient Hos => Fs.Hos;
        internal ref FileSystemClientGlobals Globals => ref Fs.Globals;

        internal FileSystemClientImpl(FileSystemClient parentClient) => Fs = parentClient;
    }

    internal struct FileSystemClientGlobals
    {
        public HorizonClient Hos;
        public object InitMutex;
        public AccessLogGlobals AccessLog;
        public UserMountTableGlobals UserMountTable;
        public FileSystemProxyServiceObjectGlobals FileSystemProxyServiceObject;
        public FsContextHandlerGlobals FsContextHandler;
        public ResultHandlingUtilityGlobals ResultHandlingUtility;
    }

    public partial class FileSystemClient
    {
        internal FileSystemClientGlobals Globals;

        public FileSystemClientImpl Impl => new FileSystemClientImpl(this);
        internal HorizonClient Hos => Globals.Hos;

        internal ITimeSpanGenerator Time { get; }
        private IAccessLog AccessLog { get; set; }

        internal MountTable MountTable { get; } = new MountTable();

        public FileSystemClient(ITimeSpanGenerator timer)
        {
            Time = timer ?? new StopWatchTimeSpanGenerator();
        }

        public FileSystemClient(HorizonClient horizonClient)
        {
            Time = horizonClient.Time;

            InitializeGlobals(horizonClient);

            Assert.NotNull(Time);
        }

        private void InitializeGlobals(HorizonClient horizonClient)
        {
            Globals.Hos = horizonClient;
            Globals.InitMutex = new object();
            Globals.UserMountTable.Initialize(this);
            Globals.FsContextHandler.Initialize(this);
        }

        public bool HasFileSystemServer()
        {
            return Hos != null;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    [DebuggerDisplay("{ToString()}")]
    internal struct MountName
    {
        public Span<byte> Name => SpanHelpers.AsByteSpan(ref this);

        public override string ToString() => new U8Span(Name).ToString();
    }
}
