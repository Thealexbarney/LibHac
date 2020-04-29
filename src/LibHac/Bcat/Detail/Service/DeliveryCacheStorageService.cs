using System;
using System.Diagnostics;
using LibHac.Bcat.Detail.Ipc;

namespace LibHac.Bcat.Detail.Service
{
    internal class DeliveryCacheStorageService : IDeliveryCacheStorageService
    {
        private const int MaxOpenCount = 8;
        private BcatServer Server { get; }

        private object Locker { get; } = new object();
        private AccessControl Access { get; }
        private ulong ApplicationId { get; }
        private int FileServiceOpenCount { get; set; }
        private int DirectoryServiceOpenCount { get; set; }

        public DeliveryCacheStorageService(BcatServer server, ulong applicationId, AccessControl accessControl)
        {
            Server = server;
            ApplicationId = applicationId;
            Access = accessControl;
        }

        public Result CreateFileService(out IDeliveryCacheFileService service)
        {
            lock (Locker)
            {
                service = default;

                if (FileServiceOpenCount >= MaxOpenCount)
                    return ResultBcat.ServiceOpenLimitReached.Log();

                service = new DeliveryCacheFileService(Server, this, ApplicationId, Access);

                FileServiceOpenCount++;
                return Result.Success;
            }
        }
        
        public Result CreateDirectoryService(out IDeliveryCacheDirectoryService service)
        {
            lock (Locker)
            {
                service = default;

                if (DirectoryServiceOpenCount >= MaxOpenCount)
                    return ResultBcat.ServiceOpenLimitReached.Log();

                service = new DeliveryCacheDirectoryService(Server, this, ApplicationId, Access);

                DirectoryServiceOpenCount++;
                return Result.Success;
            }
        }

        public Result EnumerateDeliveryCacheDirectory(out int namesRead, Span<DirectoryName> nameBuffer)
        {
            throw new NotImplementedException();
        }

        internal void NotifyCloseFile()
        {
            lock (Locker)
            {
                FileServiceOpenCount--;

                Debug.Assert(FileServiceOpenCount >= 0);
            }
        }

        internal void NotifyCloseDirectory()
        {
            lock (Locker)
            {
                DirectoryServiceOpenCount--;

                Debug.Assert(DirectoryServiceOpenCount >= 0);
            }
        }

        public void Dispose()
        {
            Server.GetStorageManager().Release(ApplicationId);
        }
    }
}
