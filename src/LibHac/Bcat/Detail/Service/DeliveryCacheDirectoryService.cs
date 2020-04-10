using System;
using LibHac.Bcat.Detail.Ipc;

namespace LibHac.Bcat.Detail.Service
{
    internal class DeliveryCacheDirectoryService : IDeliveryCacheDirectoryService
    {
        private BcatServer Server { get; }
        public object Locker { get; } = new object();
        private DeliveryCacheStorageService Parent { get; }
        private AccessControl Access { get; }
        private ulong ApplicationId { get; }
        private DirectoryName _name;
        private bool IsDirectoryOpen { get; set; }
        private int Count { get; set; }

        public DeliveryCacheDirectoryService(BcatServer server, DeliveryCacheStorageService parent, ulong applicationId,
            AccessControl accessControl)
        {
            Server = server;
            Parent = parent;
            ApplicationId = applicationId;
            Access = accessControl;
        }

        public Result Open(ref DirectoryName name)
        {
            throw new NotImplementedException();
        }

        public Result Read(ref int entriesRead, Span<DeliveryCacheDirectoryEntry> entryBuffer)
        {
            throw new NotImplementedException();
        }

        public Result GetCount(ref int count)
        {
            throw new NotImplementedException();
        }
    }
}
