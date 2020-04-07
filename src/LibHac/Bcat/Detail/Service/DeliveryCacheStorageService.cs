using System;
using LibHac.Bcat.Detail.Ipc;

namespace LibHac.Bcat.Detail.Service
{
    internal class DeliveryCacheStorageService : IDeliveryCacheStorageService
    {
        public object Locker { get; } = new object();
        private AccessControl Access { get; }
        private ulong ApplicationId { get; }
        private int OpenFileServiceCount { get; set; }
        private int OpenDirectoryServiceCount { get; set; }

        public DeliveryCacheStorageService(ulong applicationId, AccessControl accessControl)
        {
            ApplicationId = applicationId;
            Access = accessControl;
        }

        public Result CreateFileService(out IDeliveryCacheFileService fileService)
        {
            throw new NotImplementedException();
        }

        public Result CreateDirectoryService(out IDeliveryCacheDirectoryService directoryService)
        {
            throw new NotImplementedException();
        }

        public Result EnumerateDeliveryCacheDirectory(out int namesRead, Span<DirectoryName> nameBuffer)
        {
            throw new NotImplementedException();
        }
    }
}
