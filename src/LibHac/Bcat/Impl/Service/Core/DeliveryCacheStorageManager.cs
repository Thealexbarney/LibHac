using System;
using System.Diagnostics;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using static LibHac.Fs.StringTraits;

namespace LibHac.Bcat.Impl.Service.Core;

internal class DeliveryCacheStorageManager
{
    private const int MaxEntryCount = 4;

    private BcatServer Server { get; }

    private readonly object _locker = new object();
    private Entry[] Entries { get; } = new Entry[MaxEntryCount];
    private bool DisableStorage { get; set; }

    private struct Entry
    {
        public ulong ApplicationId { get; set; }
        public long RefCount { get; set; }
    }

    public DeliveryCacheStorageManager(BcatServer server)
    {
        Server = server;
        DisableStorage = false;
    }

    public Result Open(ulong applicationId)
    {
        lock (_locker)
        {
            // Find an existing storage entry for this application ID or get an empty one
            Result res = FindOrGetUnusedEntry(out int index, applicationId);
            if (res.IsFailure()) return res.Miss();

            ref Entry entry = ref Entries[index];

            if (entry.RefCount != 0)
            {
                return ResultBcat.TargetLocked.Log();
            }

            // Get the mount name
            var mountName = new MountName();

            var sb = new U8StringBuilder(mountName.Name);
            sb.Append(DeliveryCacheMountNamePrefix)
                .AppendFormat(index, 'd', 2);

            // Mount the save if enabled
            if (!DisableStorage)
            {
                res = Server.GetFsClient()
                    .MountBcatSaveData(new U8Span(mountName.Name), new Ncm.ApplicationId(applicationId));

                if (res.IsFailure())
                {
                    if (ResultFs.TargetNotFound.Includes(res))
                        return ResultBcat.SaveDataNotFound.LogConverted(res);

                    return res;
                }
            }

            // Update the storage entry
            entry.ApplicationId = applicationId;
            entry.RefCount++;

            return Result.Success;
        }
    }

    public void Release(ulong applicationId)
    {
        lock (_locker)
        {
            int index = FindEntry(applicationId);
            ref Entry entry = ref Entries[index];

            entry.RefCount--;

            // Free the entry if there are no more references
            if (entry.RefCount == 0)
            {
                var mountName = new MountName();

                var sb = new U8StringBuilder(mountName.Name);
                sb.Append(DeliveryCacheMountNamePrefix)
                    .AppendFormat(index, 'd', 2);

                // Unmount the entry's savedata
                if (!DisableStorage)
                {
                    Server.GetFsClient().Unmount(new U8Span(mountName.Name));
                }

                // Clear the entry
                entry.ApplicationId = 0;

                // todo: Call nn::bcat::detail::service::core::PassphraseManager::Remove
            }
        }
    }

    public void Commit(ulong applicationId)
    {
        lock (_locker)
        {
            int index = FindEntry(applicationId);

            var mountName = new MountName();

            var sb = new U8StringBuilder(mountName.Name);
            sb.Append(DeliveryCacheMountNamePrefix)
                .AppendFormat(index, 'd', 2);

            if (!DisableStorage)
            {
                Result res = Server.GetFsClient().Commit(new U8Span(mountName.Name));

                if (res.IsFailure())
                {
                    throw new HorizonResultException(res, "Abort");
                }
            }
        }
    }

    public Result GetFreeSpaceSize(out long size, ulong applicationId)
    {
        lock (_locker)
        {
            Span<byte> path = stackalloc byte[0x20];

            var sb = new U8StringBuilder(path);
            AppendMountName(ref sb, applicationId);
            sb.Append(RootPath);

            Result res;

            if (DisableStorage)
            {
                size = 0x4400000;
                res = Result.Success;
            }
            else
            {
                res = Server.GetFsClient().GetFreeSpaceSize(out size, new U8Span(path));
            }

            return res;
        }
    }

    public void GetPassphrasePath(Span<byte> pathBuffer, ulong applicationId)
    {
        // returns "mount:/passphrase.bin"
        lock (_locker)
        {
            var sb = new U8StringBuilder(pathBuffer);
            AppendMountName(ref sb, applicationId);
            sb.Append(PassphrasePath);
        }
    }

    public void GetDeliveryListPath(Span<byte> pathBuffer, ulong applicationId)
    {
        // returns "mount:/list.msgpack"
        lock (_locker)
        {
            var sb = new U8StringBuilder(pathBuffer);
            AppendMountName(ref sb, applicationId);
            sb.Append(DeliveryListPath);
        }
    }

    public void GetEtagFilePath(Span<byte> pathBuffer, ulong applicationId)
    {
        // returns "mount:/etag.bin"
        lock (_locker)
        {
            var sb = new U8StringBuilder(pathBuffer);
            AppendMountName(ref sb, applicationId);
            sb.Append(EtagPath);
        }
    }

    public void GetNaRequiredPath(Span<byte> pathBuffer, ulong applicationId)
    {
        // returns "mount:/na_required"
        lock (_locker)
        {
            var sb = new U8StringBuilder(pathBuffer);
            AppendMountName(ref sb, applicationId);
            sb.Append(NaRequiredPath);
        }
    }

    public void GetIndexLockPath(Span<byte> pathBuffer, ulong applicationId)
    {
        // returns "mount:/index.lock"
        lock (_locker)
        {
            var sb = new U8StringBuilder(pathBuffer);
            AppendMountName(ref sb, applicationId);
            sb.Append(IndexLockPath);
        }
    }

    public void GetFilePath(Span<byte> pathBuffer, ulong applicationId, ref DirectoryName directoryName,
        ref FileName fileName)
    {
        // returns "mount:/directories/%s/files/%s", directoryName, fileName
        lock (_locker)
        {
            var sb = new U8StringBuilder(pathBuffer);
            AppendMountName(ref sb, applicationId);

            sb.Append(DirectoriesPath)
                .Append(DirectorySeparator).Append(directoryName.Value)
                .Append(DirectorySeparator).Append(FilesDirectoryName)
                .Append(DirectorySeparator).Append(fileName.Value);
        }
    }

    public void GetFilesMetaPath(Span<byte> pathBuffer, ulong applicationId, ref DirectoryName directoryName)
    {
        // returns "mount:/directories/%s/files.meta", directoryName
        lock (_locker)
        {
            var sb = new U8StringBuilder(pathBuffer);
            AppendMountName(ref sb, applicationId);

            sb.Append(DirectoriesPath)
                .Append(DirectorySeparator).Append(directoryName.Value)
                .Append(DirectorySeparator).Append(FilesMetaFileName);
        }
    }

    public void GetDirectoriesPath(Span<byte> pathBuffer, ulong applicationId)
    {
        // returns "mount:/directories"
        lock (_locker)
        {
            var sb = new U8StringBuilder(pathBuffer);
            AppendMountName(ref sb, applicationId);
            sb.Append(DirectoriesPath);
        }
    }

    public void GetDirectoryPath(Span<byte> pathBuffer, ulong applicationId, ref DirectoryName directoryName)
    {
        // returns "mount:/directories/%s", directoryName
        lock (_locker)
        {
            var sb = new U8StringBuilder(pathBuffer);
            AppendMountName(ref sb, applicationId);

            sb.Append(DirectoriesPath)
                .Append(DirectorySeparator).Append(directoryName.Value);
        }
    }

    public void GetDirectoriesMetaPath(Span<byte> pathBuffer, ulong applicationId)
    {
        // returns "mount:/directories.meta"
        lock (_locker)
        {
            var sb = new U8StringBuilder(pathBuffer);
            AppendMountName(ref sb, applicationId);

            sb.Append(DirectoriesMetaPath);
        }
    }

    private void AppendMountName(ref U8StringBuilder sb, ulong applicationId)
    {
        int index = FindEntry(applicationId);

        sb.Append(DeliveryCacheMountNamePrefix)
            .AppendFormat(index, 'd', 2);
    }

    private Result FindOrGetUnusedEntry(out int entryIndex, ulong applicationId)
    {
        UnsafeHelpers.SkipParamInit(out entryIndex);

        // Try to find an existing entry
        for (int i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].ApplicationId == applicationId)
            {
                entryIndex = i;
                return Result.Success;
            }
        }

        // Try to find an unused entry
        for (int i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].ApplicationId == 0)
            {
                entryIndex = i;
                return Result.Success;
            }
        }

        return ResultBcat.StorageOpenLimitReached.Log();
    }

    private int FindEntry(ulong applicationId)
    {
        Entry[] entries = Entries;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].ApplicationId == applicationId)
            {
                return i;
            }
        }

        // Nintendo uses 1 as the entry index if it wasn't found
        Debug.Assert(false, "Entry not found.");
        return 1;
    }

    /// <summary>"<c>bcat-dc-</c>"</summary>
    private static ReadOnlySpan<byte> DeliveryCacheMountNamePrefix => "bcat-dc-"u8;

    /// <summary>"<c>:/</c>"</summary>
    private static ReadOnlySpan<byte> RootPath => ":/"u8;

    /// <summary>"<c>:/passphrase.bin</c>"</summary>
    private static ReadOnlySpan<byte> PassphrasePath => ":/passphrase.bin"u8;

    /// <summary>"<c>:/list.msgpack</c>"</summary>
    private static ReadOnlySpan<byte> DeliveryListPath => ":/list.msgpack"u8;

    /// <summary>"<c>:/etag.bin</c>"</summary>
    private static ReadOnlySpan<byte> EtagPath => ":/etag.bin"u8;

    /// <summary>"<c>:/na_required</c>"</summary>
    private static ReadOnlySpan<byte> NaRequiredPath => ":/na_required"u8;

    /// <summary>"<c>:/index.lock</c>"</summary>
    private static ReadOnlySpan<byte> IndexLockPath => ":/index.lock"u8;

    /// <summary>"<c>:/directories</c>"</summary>
    private static ReadOnlySpan<byte> DirectoriesPath => ":/directories"u8;

    /// <summary>"<c>files.meta</c>"</summary>
    private static ReadOnlySpan<byte> FilesMetaFileName => "files.meta"u8;

    /// <summary>"<c>:/directories.meta</c>"</summary>
    private static ReadOnlySpan<byte> DirectoriesMetaPath => ":/directories.meta"u8;

    /// <summary>"<c>files</c>"</summary>
    private static ReadOnlySpan<byte> FilesDirectoryName => "files"u8;
}