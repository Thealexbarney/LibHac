using System.Diagnostics;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Kvdb;

namespace LibHac.FsService
{
    public class SaveDataIndexer : ISaveDataIndexer
    {
        private const string LastIdFileName = "lastPublishedId";
        private const long LastIdFileSize = 8;

        private FileSystemClient FsClient { get; }
        private string MountName { get; }
        private ulong SaveDataId { get; }
        private SaveDataSpaceId SpaceId { get; }
        private KeyValueDatabase<SaveDataAttribute> KvDatabase { get; set; }
        private object Locker { get; } = new object();
        private bool IsInitialized { get; set; }
        private bool IsKvdbLoaded { get; set; }
        private long LastPublishedId { get; set; }

        public SaveDataIndexer(FileSystemClient fsClient, string mountName, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            FsClient = fsClient;
            MountName = mountName;
            SaveDataId = saveDataId;
            SpaceId = spaceId;
        }

        public Result Get(out SaveDataIndexerValue value, ref SaveDataAttribute key)
        {
            value = default;

            lock (Locker)
            {
                Result rc = Initialize();
                if (rc.IsFailure()) return rc;

                rc = EnsureKvDatabaseLoaded(false);
                if (rc.IsFailure()) return rc;

                rc = KvDatabase.Get(ref key, SpanHelpers.AsByteSpan(ref value));

                if (rc.IsFailure())
                {
                    return ResultFs.TargetNotFound.LogConverted(rc);
                }

                return Result.Success;
            }
        }

        private Result Initialize()
        {
            if (IsInitialized) return Result.Success;

            var mount = new Mounter();

            try
            {
                Result rc = mount.Initialize(FsClient, MountName, SpaceId, SaveDataId);
                if (rc.IsFailure()) return rc;

                string dbDirectory = $"{MountName}:/";

                rc = FsClient.GetEntryType(out DirectoryEntryType entryType, dbDirectory);
                if (rc.IsFailure()) return rc;

                if (entryType == DirectoryEntryType.File)
                    return ResultFs.PathNotFound.Log();

                string dbArchiveFile = $"{dbDirectory}imkvdb.arc";

                KvDatabase = new KeyValueDatabase<SaveDataAttribute>(FsClient, dbArchiveFile);

                IsInitialized = true;
                return Result.Success;
            }
            finally
            {
                mount.Dispose();
            }
        }

        private Result EnsureKvDatabaseLoaded(bool forceLoad)
        {
            Debug.Assert(KvDatabase != null);

            if (forceLoad)
            {
                IsKvdbLoaded = false;
            }
            else if (IsKvdbLoaded)
            {
                return Result.Success;
            }

            var mount = new Mounter();

            try
            {
                Result rc = mount.Initialize(FsClient, MountName, SpaceId, SaveDataId);
                if (rc.IsFailure()) return rc;

                rc = KvDatabase.ReadDatabaseFromFile();
                if (rc.IsFailure()) return rc;

                bool createdNewFile = false;
                string idFilePath = $"{MountName}:/{LastIdFileName}";

                rc = FsClient.OpenFile(out FileHandle handle, idFilePath, OpenMode.Read);

                if (rc.IsFailure())
                {
                    if (rc != ResultFs.PathNotFound) return rc;

                    rc = FsClient.CreateFile(idFilePath, LastIdFileSize);
                    if (rc.IsFailure()) return rc;

                    rc = FsClient.OpenFile(out handle, idFilePath, OpenMode.Read);
                    if (rc.IsFailure()) return rc;

                    createdNewFile = true;

                    LastPublishedId = 0;
                    IsKvdbLoaded = true;
                }

                try
                {
                    if (!createdNewFile)
                    {
                        long lastId = default;

                        rc = FsClient.ReadFile(handle, 0, SpanHelpers.AsByteSpan(ref lastId));
                        if (rc.IsFailure()) return rc;

                        LastPublishedId = lastId;
                        IsKvdbLoaded = true;
                    }

                    return Result.Success;
                }
                finally
                {
                    FsClient.CloseFile(handle);

                    if (createdNewFile)
                    {
                        FsClient.Commit(MountName);
                    }
                }
            }
            finally
            {
                mount.Dispose();
            }
        }

        private ref struct Mounter
        {
            private FileSystemClient FsClient { get; set; }
            private string MountName { get; set; }
            private bool IsMounted { get; set; }

            public Result Initialize(FileSystemClient fsClient, string mountName, SaveDataSpaceId spaceId,
                ulong saveDataId)
            {
                FsClient = fsClient;
                MountName = mountName;

                FsClient.DisableAutoSaveDataCreation();

                Result rc = FsClient.MountSystemSaveData(MountName.ToU8Span(), spaceId, saveDataId);

                if (rc.IsFailure())
                {
                    if (rc == ResultFs.TargetNotFound)
                    {
                        rc = FsClient.CreateSystemSaveData(spaceId, saveDataId, 0, 0xC0000, 0xC0000, 0);
                        if (rc.IsFailure()) return rc;

                        rc = FsClient.MountSystemSaveData(MountName.ToU8Span(), spaceId, saveDataId);
                        if (rc.IsFailure()) return rc;
                    }
                    else
                    {
                        if (ResultRangeFs.Range4771To4779.Contains(rc)) return rc;
                        if (!ResultRangeFs.DataCorrupted.Contains(rc)) return rc;

                        if (spaceId == SaveDataSpaceId.SdSystem) return rc;

                        rc = FsClient.DeleteSaveData(spaceId, saveDataId);
                        if (rc.IsFailure()) return rc;

                        rc = FsClient.CreateSystemSaveData(spaceId, saveDataId, 0, 0xC0000, 0xC0000, 0);
                        if (rc.IsFailure()) return rc;

                        rc = FsClient.MountSystemSaveData(MountName.ToU8Span(), spaceId, saveDataId);
                        if (rc.IsFailure()) return rc;
                    }
                }

                IsMounted = true;

                return Result.Success;
            }

            public void Dispose()
            {
                if (IsMounted)
                {
                    FsClient.Unmount(MountName);
                }
            }
        }
    }
}
