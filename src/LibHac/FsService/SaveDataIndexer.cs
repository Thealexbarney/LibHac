using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.Kvdb;

namespace LibHac.FsService
{
    public class SaveDataIndexer : ISaveDataIndexer
    {
        private const string LastIdFileName = "lastPublishedId";
        private const long LastIdFileSize = 8;

        private FileSystemClient FsClient { get; }
        private U8String MountName { get; }
        private ulong SaveDataId { get; }
        private SaveDataSpaceId SpaceId { get; }
        private KeyValueDatabase<SaveDataAttribute> KvDatabase { get; set; }
        private object Locker { get; } = new object();
        private bool IsInitialized { get; set; }
        private bool IsKvdbLoaded { get; set; }
        private ulong LastPublishedId { get; set; }
        private int Version { get; set; }
        private List<SaveDataInfoReader> OpenedReaders { get; } = new List<SaveDataInfoReader>();

        public SaveDataIndexer(FileSystemClient fsClient, U8Span mountName, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            FsClient = fsClient;
            MountName = mountName.ToU8String();
            SaveDataId = saveDataId;
            SpaceId = spaceId;
            Version = 1;
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
                ProgramId = key.ProgramId,
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

                    rc = FsClient.OpenFile(out FileHandle handle, idFilePath.ToU8Span(), OpenMode.Write);
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

        public Result Reset()
        {
            lock (Locker)
            {
                IsKvdbLoaded = false;

                Result rc = FsClient.DeleteSaveData(SaveDataId);

                if (rc.IsSuccess() || ResultFs.TargetNotFound.Includes(rc))
                {
                    Version++;
                }

                return rc;
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

                rc = AdjustOpenedInfoReaders(ref key);
                if (rc.IsFailure()) return rc;

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
                if (rc.IsFailure()) return rc;

                rc = AdjustOpenedInfoReaders(ref key);
                if (rc.IsFailure()) return rc;

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

                rc = KvDatabase.Delete(ref key);
                if (rc.IsFailure()) return rc;

                return AdjustOpenedInfoReaders(ref key);
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

        public int GetCount()
        {
            lock (Locker)
            {
                return KvDatabase.Count;
            }
        }

        public Result OpenSaveDataInfoReader(out ISaveDataInfoReader infoReader)
        {
            infoReader = default;

            lock (Locker)
            {
                Result rc = Initialize();
                if (rc.IsFailure()) return rc;

                rc = EnsureKvDatabaseLoaded(false);
                if (rc.IsFailure()) return rc;

                var reader = new SaveDataInfoReader(this);

                OpenedReaders.Add(reader);

                infoReader = reader;

                return Result.Success;
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

                rc = FsClient.GetEntryType(out DirectoryEntryType entryType, dbDirectory.ToU8Span());
                if (rc.IsFailure()) return rc;

                if (entryType == DirectoryEntryType.File)
                    return ResultFs.PathNotFound.Log();

                string dbArchiveFile = $"{dbDirectory}imkvdb.arc";

                KvDatabase = new KeyValueDatabase<SaveDataAttribute>(FsClient, dbArchiveFile.ToU8Span());

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
                var idFilePath = $"{MountName}:/{LastIdFileName}".ToU8String();

                rc = FsClient.OpenFile(out FileHandle handle, idFilePath, OpenMode.Read);

                if (rc.IsFailure())
                {
                    if (!ResultFs.PathNotFound.Includes(rc)) return rc;

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

        private Result AdjustOpenedInfoReaders(ref SaveDataAttribute key)
        {
            // If a new key is added or removed during iteration of the list,
            // make sure the current item of the iterator remains the same

            // Todo: A more efficient way of doing this
            List<SaveDataAttribute> list = KvDatabase.ToList().Select(x => x.key).ToList();

            int index = list.BinarySearch(key);

            bool keyWasAdded = index >= 0;

            if (!keyWasAdded)
            {
                // If the item was not found, List<T>.BinarySearch returns a negative number that
                // is the bitwise complement of the index of the next element that is larger than the item
                index = ~index;
            }

            foreach (SaveDataInfoReader reader in OpenedReaders)
            {
                if (keyWasAdded)
                {
                    // New key was inserted before the iterator's position
                    // increment the position to compensate
                    if (reader.Position >= index)
                    {
                        reader.Position++;
                    }
                }
                else
                {
                    // The position should be decremented if the iterator's position is
                    // after the key that came directly after the deleted key
                    if (reader.Position > index)
                    {
                        reader.Position--;
                    }
                }
            }

            return Result.Success;
        }

        private void CloseReader(SaveDataInfoReader reader)
        {
            // ReSharper disable once RedundantAssignment
            bool wasRemoved = OpenedReaders.Remove(reader);

            Debug.Assert(wasRemoved);
        }

        private ref struct Mounter
        {
            private FileSystemClient FsClient { get; set; }
            private U8String MountName { get; set; }
            private bool IsMounted { get; set; }

            public Result Initialize(FileSystemClient fsClient, U8String mountName, SaveDataSpaceId spaceId,
                ulong saveDataId)
            {
                FsClient = fsClient;
                MountName = mountName;

                FsClient.DisableAutoSaveDataCreation();

                Result rc = FsClient.MountSystemSaveData(MountName, spaceId, saveDataId);

                if (rc.IsFailure())
                {
                    if (ResultFs.TargetNotFound.Includes(rc))
                    {
                        rc = FsClient.CreateSystemSaveData(spaceId, saveDataId, 0, 0xC0000, 0xC0000, 0);
                        if (rc.IsFailure()) return rc;

                        rc = FsClient.MountSystemSaveData(MountName, spaceId, saveDataId);
                        if (rc.IsFailure()) return rc;
                    }
                    else
                    {
                        if (ResultFs.SignedSystemPartitionDataCorrupted.Includes(rc)) return rc;
                        if (!ResultFs.DataCorrupted.Includes(rc)) return rc;

                        if (spaceId == SaveDataSpaceId.SdSystem) return rc;

                        rc = FsClient.DeleteSaveData(spaceId, saveDataId);
                        if (rc.IsFailure()) return rc;

                        rc = FsClient.CreateSystemSaveData(spaceId, saveDataId, 0, 0xC0000, 0xC0000, 0);
                        if (rc.IsFailure()) return rc;

                        rc = FsClient.MountSystemSaveData(MountName, spaceId, saveDataId);
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

        private class SaveDataInfoReader : ISaveDataInfoReader
        {
            private SaveDataIndexer Indexer { get; }
            private int Version { get; }
            public int Position { get; set; }

            public SaveDataInfoReader(SaveDataIndexer indexer)
            {
                Indexer = indexer;
                Version = indexer.Version;
            }

            public Result ReadSaveDataInfo(out long readCount, Span<byte> saveDataInfoBuffer)
            {
                readCount = default;

                lock (Indexer.Locker)
                {
                    // Indexer has been reloaded since this info reader was created
                    if (Version != Indexer.Version)
                    {
                        return ResultFs.InvalidSaveDataInfoReader.Log();
                    }

                    // No more to iterate
                    if (Position == Indexer.KvDatabase.Count)
                    {
                        readCount = 0;
                        return Result.Success;
                    }

                    Span<SaveDataInfo> outInfo = MemoryMarshal.Cast<byte, SaveDataInfo>(saveDataInfoBuffer);

                    // Todo: A more efficient way of doing this
                    List<(SaveDataAttribute key, byte[] value)> list = Indexer.KvDatabase.ToList();

                    int i;
                    for (i = 0; i < outInfo.Length && Position < list.Count; i++, Position++)
                    {
                        SaveDataAttribute key = list[Position].key;
                        ref SaveDataIndexerValue value = ref Unsafe.As<byte, SaveDataIndexerValue>(ref list[Position].value[0]);

                        GetSaveDataInfo(out outInfo[i], ref key, ref value);
                    }

                    readCount = i;

                    return Result.Success;
                }
            }

            public void Dispose()
            {
                Indexer?.CloseReader(this);
            }
        }
    }
}
