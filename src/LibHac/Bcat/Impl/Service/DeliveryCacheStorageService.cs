using System;
using System.Diagnostics;
using LibHac.Bcat.Impl.Ipc;
using LibHac.Bcat.Impl.Service.Core;
using LibHac.Common;
using LibHac.Util;

namespace LibHac.Bcat.Impl.Service;

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

    public Result CreateFileService(ref SharedRef<IDeliveryCacheFileService> outService)
    {
        lock (Locker)
        {
            if (FileServiceOpenCount >= MaxOpenCount)
                return ResultBcat.ServiceOpenLimitReached.Log();

            outService.Reset(new DeliveryCacheFileService(Server, this, ApplicationId, Access));

            FileServiceOpenCount++;
            return Result.Success;
        }
    }

    public Result CreateDirectoryService(ref SharedRef<IDeliveryCacheDirectoryService> outService)
    {
        lock (Locker)
        {
            if (DirectoryServiceOpenCount >= MaxOpenCount)
                return ResultBcat.ServiceOpenLimitReached.Log();

            outService.Reset(new DeliveryCacheDirectoryService(Server, this, ApplicationId, Access));

            DirectoryServiceOpenCount++;
            return Result.Success;
        }
    }

    public Result EnumerateDeliveryCacheDirectory(out int namesRead, Span<DirectoryName> nameBuffer)
    {
        UnsafeHelpers.SkipParamInit(out namesRead);

        lock (Locker)
        {
            var metaReader = new DeliveryCacheDirectoryMetaAccessor(Server);
            Result res = metaReader.ReadApplicationDirectoryMeta(ApplicationId, true);
            if (res.IsFailure()) return res.Miss();

            int i;
            for (i = 0; i < nameBuffer.Length; i++)
            {
                res = metaReader.GetEntry(out DeliveryCacheDirectoryMetaEntry entry, i);

                if (res.IsFailure())
                {
                    if (!ResultBcat.NotFound.Includes(res))
                        return res;

                    break;
                }

                StringUtils.Copy(nameBuffer[i].Value, entry.Name.Value);
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