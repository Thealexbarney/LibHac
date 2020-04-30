﻿using System;
using System.Diagnostics;
using LibHac.Bcat.Detail.Ipc;
using LibHac.Bcat.Detail.Service.Core;
using LibHac.Common;

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
            lock (Locker)
            {
                namesRead = default;

                var metaReader = new DeliveryCacheDirectoryMetaAccessor(Server);
                Result rc = metaReader.ReadApplicationDirectoryMeta(ApplicationId, true);
                if (rc.IsFailure()) return rc;

                int i;
                for (i = 0; i < nameBuffer.Length; i++)
                {
                    rc = metaReader.GetEntry(out DeliveryCacheDirectoryMetaEntry entry, i);

                    if (rc.IsFailure())
                    {
                        if (!ResultBcat.NotFound.Includes(rc))
                            return rc;

                        break;
                    }

                    StringUtils.Copy(nameBuffer[i].Bytes, entry.Name.Bytes);
                }

                namesRead = i;
                return Result.Success;
            }
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
