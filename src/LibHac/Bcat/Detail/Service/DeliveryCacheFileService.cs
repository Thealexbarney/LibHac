using System;
using LibHac.Bcat.Detail.Ipc;
using LibHac.Fs;

namespace LibHac.Bcat.Detail.Service
{
    internal class DeliveryCacheFileService : IDeliveryCacheFileService
    {
        public object Locker { get; } = new object();
        private DeliveryCacheStorageService Parent { get; }
        private AccessControl Access { get; }
        private ulong ApplicationId { get; }
        private FileHandle Handle { get; set; }
        private DeliveryCacheFileEntryMeta _metaEntry;
        private bool IsFileOpen { get; set; }

        public DeliveryCacheFileService(DeliveryCacheStorageService parent, ulong applicationId,
            AccessControl accessControl)
        {
            Parent = parent;
            ApplicationId = applicationId;
            Access = accessControl;
        }

        public Result Open(ref DirectoryName directoryName, ref FileName fileName)
        {
            throw new NotImplementedException();
        }

        public Result Read(out long bytesRead, long offset, Span<byte> destination)
        {
            throw new NotImplementedException();
        }

        public Result GetSize(out long size)
        {
            throw new NotImplementedException();
        }

        public Result GetDigest(out Digest digest)
        {
            throw new NotImplementedException();
        }
    }
}
