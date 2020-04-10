using System;
using LibHac.Bcat.Detail.Ipc;
using LibHac.Bcat.Detail.Service.Core;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.Bcat.Detail.Service
{
    internal class DeliveryCacheFileService : IDeliveryCacheFileService, IDisposable
    {
        private BcatServer Server { get; }
        private object Locker { get; } = new object();
        private DeliveryCacheStorageService Parent { get; }
        private AccessControl Access { get; }
        private ulong ApplicationId { get; }
        private FileHandle _handle;
        private DeliveryCacheFileMetaEntry _metaEntry;
        private bool IsFileOpen { get; set; }

        public DeliveryCacheFileService(BcatServer server, DeliveryCacheStorageService parent, ulong applicationId,
            AccessControl accessControl)
        {
            Server = server;
            Parent = parent;
            ApplicationId = applicationId;
            Access = accessControl;
        }

        public Result Open(ref DirectoryName directoryName, ref FileName fileName)
        {
            if (!directoryName.IsValid())
                return ResultBcat.InvalidArgument.Log();

            if (!fileName.IsValid())
                return ResultBcat.InvalidArgument.Log();

            lock (Locker)
            {
                if (IsFileOpen)
                    return ResultBcat.AlreadyOpen.Log();

                var metaReader = new DeliveryCacheFileMetaAccessor(Server);
                Result rc = metaReader.ReadApplicationFileMeta(ApplicationId, ref directoryName, true);
                if (rc.IsFailure()) return rc;

                rc = metaReader.FindEntry(out DeliveryCacheFileMetaEntry entry, ref fileName);
                if (rc.IsFailure()) return rc;

                Span<byte> filePath = stackalloc byte[0x80];
                Server.GetStorageManager().GetFilePath(filePath, ApplicationId, ref directoryName, ref fileName);

                rc = Server.GetFsClient().OpenFile(out _handle, new U8Span(filePath), OpenMode.Read);
                if (rc.IsFailure()) return rc;

                _metaEntry = entry;
                IsFileOpen = true;

                return Result.Success;
            }
        }

        public Result Read(out long bytesRead, long offset, Span<byte> destination)
        {
            lock (Locker)
            {
                bytesRead = 0;

                if (!IsFileOpen)
                    return ResultBcat.NotOpen.Log();

                Result rc = Server.GetFsClient().ReadFile(out long read, _handle, offset, destination);
                if (rc.IsFailure()) return rc;

                bytesRead = read;
                return Result.Success;
            }
        }

        public Result GetSize(out long size)
        {
            lock (Locker)
            {
                if (!IsFileOpen)
                {
                    size = default;
                    return ResultBcat.NotOpen.Log();
                }

                return Server.GetFsClient().GetFileSize(out size, _handle);
            }
        }

        public Result GetDigest(out Digest digest)
        {
            lock (Locker)
            {
                if (!IsFileOpen)
                {
                    digest = default;
                    return ResultBcat.NotOpen.Log();
                }

                digest = _metaEntry.Digest;
                return Result.Success;
            }
        }

        public void Dispose()
        {
            if (IsFileOpen)
            {
                Server.GetFsClient().CloseFile(_handle);
            }

            Parent.NotifyCloseFile();
        }
    }
}
