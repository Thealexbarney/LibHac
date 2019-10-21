using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.Kvdb;
using LibHac.Ncm;

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
        private ulong LastPublishedId { get; set; }

        public SaveDataIndexer(FileSystemClient fsClient, string mountName, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            FsClient = fsClient;
            MountName = mountName;
            SaveDataId = saveDataId;
            SpaceId = spaceId;
        }

        public static void GetSaveDataInfo(out SaveDataInfo info, ref SaveDataAttribute key, ref SaveDataIndexerValue value)
        {
            info = new SaveDataInfo
            {
                SaveDataId = value.SaveDataId,
                SpaceId = value.SpaceId,
                Type = key.Type,
                UserId = key.UserId,
                SaveDataIdFromKey = key.SaveDataId,
                TitleId = key.TitleId,
                Size = value.Size,
                Index = key.Index,
                Rank = key.Rank,
                State = value.State
            };
        }

        public Result Commit()
        {
            lock (Locker)
            {
                Result rc = Initialize();
                if (rc.IsFailure()) return rc;

                rc = EnsureKvDatabaseLoaded(false);
                if (rc.IsFailure()) return rc;

                var mount = new Mounter();

                try
                {
                    rc = mount.Initialize(FsClient, MountName, SpaceId, SaveDataId);
                    if (rc.IsFailure()) return rc;

                    rc = KvDatabase.WriteDatabaseToFile();
                    if (rc.IsFailure()) return rc;

                    string idFilePath = $"{MountName}:/{LastIdFileName}";

                    rc = FsClient.OpenFile(out FileHandle handle, idFilePath, OpenMode.Write);
                    if (rc.IsFailure()) return rc;

                    bool fileAlreadyClosed = false;

                    try
                    {
                        ulong lastId = LastPublishedId;

                        rc = FsClient.WriteFile(handle, 0, SpanHelpers.AsByteSpan(ref lastId), WriteOption.None);
                        if (rc.IsFailure()) return rc;

                        rc = FsClient.FlushFile(handle);
                        if (rc.IsFailure()) return rc;

                        FsClient.CloseFile(handle);
                        fileAlreadyClosed = true;

                        return FsClient.Commit(MountName);
                    }
                    finally
                    {
                        if (!fileAlreadyClosed)
                        {
                            FsClient.CloseFile(handle);
                        }
                    }
                }
                finally
                {
                    mount.Dispose();
                }
            }
        }

        public Result Add(out ulong saveDataId, ref SaveDataAttribute key)
        {
            saveDataId = default;

            lock (Locker)
            {
                Result rc = Initialize();
                if (rc.IsFailure()) return rc;

                rc = EnsureKvDatabaseLoaded(false);
                if (rc.IsFailure()) return rc;

                SaveDataIndexerValue value = default;

                rc = KvDatabase.Get(ref key, SpanHelpers.AsByteSpan(ref value));

                if (rc.IsSuccess())
                {
                    return ResultFs.SaveDataPathAlreadyExists.Log();
                }

                LastPublishedId++;
                ulong newSaveDataId = LastPublishedId;

                value = new SaveDataIndexerValue { SaveDataId = newSaveDataId };

                rc = KvDatabase.Set(ref key, SpanHelpers.AsByteSpan(ref value));

                if (rc.IsFailure())
                {
                    LastPublishedId--;
                    return rc;
                }

                saveDataId = newSaveDataId;
                return Result.Success;
            }
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

        public Result AddSystemSaveData(ref SaveDataAttribute key)
        {
            lock (Locker)
            {
                Result rc = Initialize();
                if (rc.IsFailure()) return rc;

                rc = EnsureKvDatabaseLoaded(false);
                if (rc.IsFailure()) return rc;

                foreach (KeyValuePair<SaveDataAttribute, byte[]> kvp in KvDatabase)
                {
                    ref SaveDataIndexerValue value = ref Unsafe.As<byte, SaveDataIndexerValue>(ref kvp.Value[0]);

                    if (key.SaveDataId == value.SaveDataId)
                    {
                        return ResultFs.SaveDataPathAlreadyExists.Log();
                    }
                }

                var newValue = new SaveDataIndexerValue
                {
                    SaveDataId = key.SaveDataId
                };

                rc = KvDatabase.Set(ref key, SpanHelpers.AsByteSpan(ref newValue));

                if (rc.IsFailure())
                {
                    // todo: Missing some function call here
                }

                return rc;
            }
        }

        public bool IsFull()
        {
            return false;
        }

        public Result Delete(ulong saveDataId)
        {
            lock (Locker)
            {
                Result rc = Initialize();
                if (rc.IsFailure()) return rc;

                rc = EnsureKvDatabaseLoaded(false);
                if (rc.IsFailure()) return rc;

                if (!TryGetBySaveDataIdInternal(out SaveDataAttribute key, out _, saveDataId))
                {
                    return ResultFs.TargetNotFound.Log();
                }

                return KvDatabase.Delete(ref key);
            }
        }

        public Result SetSpaceId(ulong saveDataId, SaveDataSpaceId spaceId)
        {
            lock (Locker)
            {
                Result rc = Initialize();
                if (rc.IsFailure()) return rc;

                rc = EnsureKvDatabaseLoaded(false);
                if (rc.IsFailure()) return rc;

                if (!TryGetBySaveDataIdInternal(out SaveDataAttribute key, out SaveDataIndexerValue value, saveDataId))
                {
                    return ResultFs.TargetNotFound.Log();
                }

                value.SpaceId = spaceId;

                return KvDatabase.Set(ref key, SpanHelpers.AsByteSpan(ref value));
            }
        }

        public Result SetSize(ulong saveDataId, long size)
        {
            lock (Locker)
            {
                Result rc = Initialize();
                if (rc.IsFailure()) return rc;

                rc = EnsureKvDatabaseLoaded(false);
                if (rc.IsFailure()) return rc;

                if (!TryGetBySaveDataIdInternal(out SaveDataAttribute key, out SaveDataIndexerValue value, saveDataId))
                {
                    return ResultFs.TargetNotFound.Log();
                }

                value.Size = size;

                return KvDatabase.Set(ref key, SpanHelpers.AsByteSpan(ref value));
            }
        }

        public Result SetState(ulong saveDataId, SaveDataState state)
        {
            lock (Locker)
            {
                Result rc = Initialize();
                if (rc.IsFailure()) return rc;

                rc = EnsureKvDatabaseLoaded(false);
                if (rc.IsFailure()) return rc;

                if (!TryGetBySaveDataIdInternal(out SaveDataAttribute key, out SaveDataIndexerValue value, saveDataId))
                {
                    return ResultFs.TargetNotFound.Log();
                }

                value.State = state;

                return KvDatabase.Set(ref key, SpanHelpers.AsByteSpan(ref value));
            }
        }

        public Result GetKey(out SaveDataAttribute key, ulong saveDataId)
        {
            key = default;

            lock (Locker)
            {
                Result rc = Initialize();
                if (rc.IsFailure()) return rc;

                rc = EnsureKvDatabaseLoaded(false);
                if (rc.IsFailure()) return rc;

                if (TryGetBySaveDataIdInternal(out key, out _, saveDataId))
                {
                    return Result.Success;
                }

                return ResultFs.TargetNotFound.Log();
            }
        }

        public Result GetBySaveDataId(out SaveDataIndexerValue value, ulong saveDataId)
        {
            value = default;

            lock (Locker)
            {
                Result rc = Initialize();
                if (rc.IsFailure()) return rc;

                rc = EnsureKvDatabaseLoaded(false);
                if (rc.IsFailure()) return rc;

                if (TryGetBySaveDataIdInternal(out _, out value, saveDataId))
                {
                    return Result.Success;
                }

                return ResultFs.TargetNotFound.Log();
            }
        }

        private bool TryGetBySaveDataIdInternal(out SaveDataAttribute key, out SaveDataIndexerValue value, ulong saveDataId)
        {
            foreach (KeyValuePair<SaveDataAttribute, byte[]> kvp in KvDatabase)
            {
                ref SaveDataIndexerValue currentValue = ref Unsafe.As<byte, SaveDataIndexerValue>(ref kvp.Value[0]);

                if (currentValue.SaveDataId == saveDataId)
                {
                    key = kvp.Key;
                    value = currentValue;
                    return true;
                }
            }

            key = default;
            value = default;
            return false;
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
                        ulong lastId = default;

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
                        rc = FsClient.CreateSystemSaveData(spaceId, saveDataId, TitleId.Zero, 0xC0000, 0xC0000, 0);
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

                        rc = FsClient.CreateSystemSaveData(spaceId, saveDataId, TitleId.Zero, 0xC0000, 0xC0000, 0);
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
