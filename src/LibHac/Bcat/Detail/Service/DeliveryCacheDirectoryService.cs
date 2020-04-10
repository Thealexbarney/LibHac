using System;
using LibHac.Bcat.Detail.Ipc;
using LibHac.Bcat.Detail.Service.Core;

namespace LibHac.Bcat.Detail.Service
{
    internal class DeliveryCacheDirectoryService : IDeliveryCacheDirectoryService, IDisposable
    {
        private BcatServer Server { get; }
        private object Locker { get; } = new object();
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
            if (!name.IsValid())
                return ResultBcat.InvalidArgument.Log();

            lock (Locker)
            {
                if (IsDirectoryOpen)
                    return ResultBcat.AlreadyOpen.Log();

                var metaReader = new DeliveryCacheFileMetaAccessor(Server);
                Result rc = metaReader.ReadApplicationFileMeta(ApplicationId, ref name, false);
                if (rc.IsFailure()) return rc;

                Count = metaReader.Count;
                _name = name;
                IsDirectoryOpen = true;

                return Result.Success;
            }
        }

        public Result Read(out int entriesRead, Span<DeliveryCacheDirectoryEntry> entryBuffer)
        {
            lock (Locker)
            {
                entriesRead = default;

                if (!IsDirectoryOpen)
                    return ResultBcat.NotOpen.Log();

                var metaReader = new DeliveryCacheFileMetaAccessor(Server);
                Result rc = metaReader.ReadApplicationFileMeta(ApplicationId, ref _name, true);
                if (rc.IsFailure()) return rc;

                int i;
                for (i = 0; i < entryBuffer.Length; i++)
                {
                    rc = metaReader.GetEntry(out DeliveryCacheFileMetaEntry entry, i);

                    if (rc.IsFailure())
                    {
                        if (!ResultBcat.NotFound.Includes(rc))
                            return rc;

                        break;
                    }

                    entryBuffer[i] = new DeliveryCacheDirectoryEntry(ref entry.Name, entry.Size, ref entry.Digest);
                }

                entriesRead = i;
                return Result.Success;
            }
        }

        public Result GetCount(out int count)
        {
            lock (Locker)
            {
                if (!IsDirectoryOpen)
                {
                    count = default;
                    return ResultBcat.NotOpen.Log();
                }

                count = Count;
                return Result.Success;
            }
        }

        public void Dispose()
        {
            Parent.NotifyCloseDirectory();
        }
    }
}
