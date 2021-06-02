using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Sf;
using LibHac.Kvdb;
using LibHac.Sf;

namespace LibHac.FsSrv
{
    /// <summary>
    /// Indexes metadata for persistent save data stored on disk, holding key-value pairs of types
    /// <see cref="SaveDataAttribute"/> and <see cref="SaveDataIndexerValue"/> respectively.
    /// </summary>
    /// <remarks>
    /// Each <see cref="SaveDataIndexer"/> manages one to two save data spaces.
    /// Each save data space is identified by a <see cref="SaveDataSpaceId"/>,
    /// and has its own unique storage location on disk.
    /// <br/>Based on FS 10.0.0 (nnSdk 10.4.0)
    /// </remarks>
    public class SaveDataIndexer : ISaveDataIndexer
    {
        private const int KvDatabaseCapacity = 0x1080;
        private const int KvDatabaseReservedEntryCount = 0x80;

        private const int SaveDataAvailableSize = 0xC0000;
        private const int SaveDataJournalSize = 0xC0000;

        private const long LastPublishedIdFileSize = sizeof(long);
        private const int MaxPathLength = 0x30;

        private static ReadOnlySpan<byte> LastPublishedIdFileName => // lastPublishedId
            new[]
            {
                (byte) 'l', (byte) 'a', (byte) 's', (byte) 't', (byte) 'P', (byte) 'u', (byte) 'b', (byte) 'l',
                (byte) 'i', (byte) 's', (byte) 'h', (byte) 'e', (byte) 'd', (byte) 'I', (byte) 'd'
            };

        private static ReadOnlySpan<byte> MountDelimiter => // :/
            new[] { (byte)':', (byte)'/' };

        private delegate void SaveDataValueTransform(ref SaveDataIndexerValue value, ReadOnlySpan<byte> updateData);

        private FileSystemClient FsClient { get; }
        private U8String MountName { get; }
        private ulong SaveDataId { get; }
        private SaveDataSpaceId SpaceId { get; }
        private MemoryResource MemoryResource { get; }
        private MemoryResource BufferMemoryResource { get; }

        private FlatMapKeyValueStore<SaveDataAttribute> KvDatabase { get; }

        private object Locker { get; } = new object();
        private bool IsInitialized { get; set; }
        private bool IsKvdbLoaded { get; set; }
        private ulong _lastPublishedId;
        private int Handle { get; set; }

        private List<ReaderAccessor> OpenReaders { get; } = new List<ReaderAccessor>();

        public SaveDataIndexer(FileSystemClient fsClient, U8Span mountName, SaveDataSpaceId spaceId, ulong saveDataId, MemoryResource memoryResource)
        {
            FsClient = fsClient;
            SaveDataId = saveDataId;
            SpaceId = spaceId;
            MemoryResource = memoryResource;

            // note: FS uses a separate PooledBufferMemoryResource here
            BufferMemoryResource = memoryResource;

            KvDatabase = new FlatMapKeyValueStore<SaveDataAttribute>();

            IsInitialized = false;
            IsKvdbLoaded = false;
            Handle = 1;

            MountName = mountName.ToU8String();
        }

        private static void MakeLastPublishedIdSaveFilePath(Span<byte> buffer, ReadOnlySpan<byte> mountName)
        {
            // returns "%s:/%s", mountName, "lastPublishedId"
            var sb = new U8StringBuilder(buffer);
            sb.Append(mountName);
            sb.Append(MountDelimiter);
            sb.Append(LastPublishedIdFileName);

            Debug.Assert(!sb.Overflowed);
        }

        private static void MakeRootPath(Span<byte> buffer, ReadOnlySpan<byte> mountName)
        {
            // returns "%s:/", mountName
            var sb = new U8StringBuilder(buffer);
            sb.Append(mountName);
            sb.Append(MountDelimiter);

            Debug.Assert(!sb.Overflowed);
        }

        /// <summary>
        /// Generates a <see cref="SaveDataInfo"/> from the provided <see cref="SaveDataAttribute"/> and <see cref="SaveDataIndexerValue"/>.
        /// </summary>
        /// <param name="info">When this method returns, contains the generated <see cref="SaveDataInfo"/>.</param>
        /// <param name="key">The key used to generate the <see cref="SaveDataInfo"/>.</param>
        /// <param name="value">The value used to generate the <see cref="SaveDataInfo"/>.</param>
        public static void GenerateSaveDataInfo(out SaveDataInfo info, in SaveDataAttribute key, in SaveDataIndexerValue value)
        {
            info = new SaveDataInfo
            {
                SaveDataId = value.SaveDataId,
                SpaceId = value.SpaceId,
                Type = key.Type,
                UserId = key.UserId,
                StaticSaveDataId = key.StaticSaveDataId,
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
                Result rc = TryInitializeDatabase();
                if (rc.IsFailure()) return rc;

                rc = TryLoadDatabase(false);
                if (rc.IsFailure()) return rc;

                var mount = new Mounter();

                try
                {
                    rc = mount.Mount(FsClient, MountName, SpaceId, SaveDataId);
                    if (rc.IsFailure()) return rc;

                    rc = KvDatabase.Save();
                    if (rc.IsFailure()) return rc;

                    Span<byte> lastPublishedIdPath = stackalloc byte[MaxPathLength];
                    MakeLastPublishedIdSaveFilePath(lastPublishedIdPath, MountName);

                    rc = FsClient.OpenFile(out FileHandle handle, new U8Span(lastPublishedIdPath), OpenMode.Write);
                    if (rc.IsFailure()) return rc;

                    bool isFileClosed = false;

                    try
                    {
                        rc = FsClient.WriteFile(handle, 0, SpanHelpers.AsByteSpan(ref _lastPublishedId), WriteOption.None);
                        if (rc.IsFailure()) return rc;

                        rc = FsClient.FlushFile(handle);
                        if (rc.IsFailure()) return rc;

                        FsClient.CloseFile(handle);
                        isFileClosed = true;

                        return FsClient.Commit(MountName);
                    }
                    finally
                    {
                        if (!isFileClosed)
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

        public Result Rollback()
        {
            lock (Locker)
            {
                Result rc = TryInitializeDatabase();
                if (rc.IsFailure()) return rc;

                rc = TryLoadDatabase(forceLoad: true);
                if (rc.IsFailure()) return rc;

                UpdateHandle();
                return Result.Success;
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
                    UpdateHandle();
                }

                return rc;
            }
        }

        public Result Publish(out ulong saveDataId, in SaveDataAttribute key)
        {
            UnsafeHelpers.SkipParamInit(out saveDataId);

            lock (Locker)
            {
                Result rc = TryInitializeDatabase();
                if (rc.IsFailure()) return rc;

                rc = TryLoadDatabase(false);
                if (rc.IsFailure()) return rc;

                Unsafe.SkipInit(out SaveDataIndexerValue value);

                // Make sure the key isn't in the database already.
                rc = KvDatabase.Get(out _, in key, SpanHelpers.AsByteSpan(ref value));

                if (rc.IsSuccess())
                {
                    return ResultFs.AlreadyExists.Log();
                }

                _lastPublishedId++;
                ulong newSaveDataId = _lastPublishedId;

                value = new SaveDataIndexerValue { SaveDataId = newSaveDataId };

                rc = KvDatabase.Set(in key, SpanHelpers.AsByteSpan(ref value));

                if (rc.IsFailure())
                {
                    _lastPublishedId--;
                    return rc;
                }

                rc = FixReader(in key);
                if (rc.IsFailure()) return rc;

                saveDataId = newSaveDataId;
                return Result.Success;
            }
        }

        public Result Get(out SaveDataIndexerValue value, in SaveDataAttribute key)
        {
            UnsafeHelpers.SkipParamInit(out value);

            lock (Locker)
            {
                Result rc = TryInitializeDatabase();
                if (rc.IsFailure()) return rc;

                rc = TryLoadDatabase(false);
                if (rc.IsFailure()) return rc;

                rc = KvDatabase.Get(out _, in key, SpanHelpers.AsByteSpan(ref value));

                if (rc.IsFailure())
                {
                    return ResultFs.TargetNotFound.LogConverted(rc);
                }

                return Result.Success;
            }
        }

        public Result PutStaticSaveDataIdIndex(in SaveDataAttribute key)
        {
            lock (Locker)
            {
                Result rc = TryInitializeDatabase();
                if (rc.IsFailure()) return rc;

                rc = TryLoadDatabase(false);
                if (rc.IsFailure()) return rc;

                // Iterate through all existing values to check if the save ID is already in use.
                FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator = KvDatabase.GetBeginIterator();
                while (!iterator.IsEnd())
                {
                    if (iterator.GetValue<SaveDataIndexerValue>().SaveDataId == key.StaticSaveDataId)
                    {
                        return ResultFs.AlreadyExists.Log();
                    }

                    iterator.Next();
                }

                var newValue = new SaveDataIndexerValue
                {
                    SaveDataId = key.StaticSaveDataId
                };

                rc = KvDatabase.Set(in key, SpanHelpers.AsReadOnlyByteSpan(in newValue));
                if (rc.IsFailure()) return rc;

                rc = FixReader(in key);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
        }

        public bool IsRemainedReservedOnly()
        {
            return KvDatabase.Count >= KvDatabaseCapacity - KvDatabaseReservedEntryCount;
        }

        public Result Delete(ulong saveDataId)
        {
            lock (Locker)
            {
                Result rc = TryInitializeDatabase();
                if (rc.IsFailure()) return rc;

                rc = TryLoadDatabase(false);
                if (rc.IsFailure()) return rc;

                FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator = KvDatabase.GetBeginIterator();

                while (true)
                {
                    if (iterator.IsEnd())
                        return ResultFs.TargetNotFound.Log();

                    if (iterator.GetValue<SaveDataIndexerValue>().SaveDataId == saveDataId)
                        break;

                    iterator.Next();
                }

                SaveDataAttribute key = iterator.Get().Key;

                rc = KvDatabase.Delete(in key);
                if (rc.IsFailure()) return rc;

                rc = FixReader(in key);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
        }

        private Result UpdateValueBySaveDataId(ulong saveDataId, SaveDataValueTransform func, ReadOnlySpan<byte> data)
        {
            lock (Locker)
            {
                Result rc = TryInitializeDatabase();
                if (rc.IsFailure()) return rc;

                rc = TryLoadDatabase(false);
                if (rc.IsFailure()) return rc;

                SaveDataIndexerValue value;
                FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator = KvDatabase.GetBeginIterator();

                while (true)
                {
                    if (iterator.IsEnd())
                        return ResultFs.TargetNotFound.Log();

                    ref SaveDataIndexerValue val = ref iterator.GetValue<SaveDataIndexerValue>();

                    if (val.SaveDataId == saveDataId)
                    {
                        value = val;
                        break;
                    }

                    iterator.Next();
                }

                func(ref value, data);

                rc = KvDatabase.Set(in iterator.Get().Key, SpanHelpers.AsReadOnlyByteSpan(in value));
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
        }

        public Result SetSpaceId(ulong saveDataId, SaveDataSpaceId spaceId)
        {
            return UpdateValueBySaveDataId(saveDataId, SetSpaceIdImpl, SpanHelpers.AsReadOnlyByteSpan(in spaceId));

            static void SetSpaceIdImpl(ref SaveDataIndexerValue value, ReadOnlySpan<byte> updateData)
            {
                value.SpaceId = (SaveDataSpaceId)updateData[0];
            }
        }

        public Result SetSize(ulong saveDataId, long size)
        {
            return UpdateValueBySaveDataId(saveDataId, SetSizeImpl, SpanHelpers.AsReadOnlyByteSpan(in size));

            static void SetSizeImpl(ref SaveDataIndexerValue value, ReadOnlySpan<byte> updateData)
            {
                value.Size = BinaryPrimitives.ReadInt64LittleEndian(updateData);
            }
        }

        public Result SetState(ulong saveDataId, SaveDataState state)
        {
            return UpdateValueBySaveDataId(saveDataId, SetSizeImpl, SpanHelpers.AsReadOnlyByteSpan(in state));

            static void SetSizeImpl(ref SaveDataIndexerValue value, ReadOnlySpan<byte> updateData)
            {
                value.State = (SaveDataState)updateData[0];
            }
        }

        public Result GetKey(out SaveDataAttribute key, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out key);

            Result rc = TryInitializeDatabase();
            if (rc.IsFailure()) return rc;

            rc = TryLoadDatabase(false);
            if (rc.IsFailure()) return rc;

            FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator = KvDatabase.GetBeginIterator();

            while (!iterator.IsEnd())
            {
                if (iterator.GetValue<SaveDataIndexerValue>().SaveDataId == saveDataId)
                {
                    key = iterator.Get().Key;
                    return Result.Success;
                }

                iterator.Next();
            }

            return ResultFs.TargetNotFound.Log();
        }

        public Result GetValue(out SaveDataIndexerValue value, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out value);

            Result rc = TryInitializeDatabase();
            if (rc.IsFailure()) return rc;

            rc = TryLoadDatabase(false);
            if (rc.IsFailure()) return rc;

            FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator = KvDatabase.GetBeginIterator();

            while (!iterator.IsEnd())
            {
                ref SaveDataIndexerValue val = ref iterator.GetValue<SaveDataIndexerValue>();

                if (val.SaveDataId == saveDataId)
                {
                    value = val;
                    return Result.Success;
                }

                iterator.Next();
            }

            return ResultFs.TargetNotFound.Log();
        }

        public Result SetValue(in SaveDataAttribute key, in SaveDataIndexerValue value)
        {
            Result rc = TryInitializeDatabase();
            if (rc.IsFailure()) return rc;

            rc = TryLoadDatabase(false);
            if (rc.IsFailure()) return rc;

            FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator = KvDatabase.GetLowerBoundIterator(in key);

            // Key was not found
            if (iterator.IsEnd())
                return ResultFs.TargetNotFound.Log();

            iterator.GetValue<SaveDataIndexerValue>() = value;
            return Result.Success;
        }

        public int GetIndexCount()
        {
            lock (Locker)
            {
                return KvDatabase.Count;
            }
        }

        public Result OpenSaveDataInfoReader(out ReferenceCountedDisposable<SaveDataInfoReaderImpl> infoReader)
        {
            UnsafeHelpers.SkipParamInit(out infoReader);

            lock (Locker)
            {
                Result rc = TryInitializeDatabase();
                if (rc.IsFailure()) return rc;

                rc = TryLoadDatabase(false);
                if (rc.IsFailure()) return rc;

                // Create the reader and register it in the opened-reader list
                using (var reader = new ReferenceCountedDisposable<Reader>(new Reader(this)))
                {
                    rc = RegisterReader(reader);
                    if (rc.IsFailure()) return rc;

                    infoReader = reader.AddReference<SaveDataInfoReaderImpl>();
                    return Result.Success;
                }
            }
        }

        private FlatMapKeyValueStore<SaveDataAttribute>.Iterator GetBeginIterator()
        {
            Assert.SdkRequires(IsKvdbLoaded);

            return KvDatabase.GetBeginIterator();
        }

        private void FixIterator(ref FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator,
            in SaveDataAttribute key)
        {
            KvDatabase.FixIterator(ref iterator, in key);
        }

        /// <summary>
        /// Initializes <see cref="KvDatabase"/> and ensures that the indexer's save data is created.
        /// Does nothing if this <see cref="SaveDataIndexer"/> has already been initialized.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private Result TryInitializeDatabase()
        {
            if (IsInitialized) return Result.Success;

            var mount = new Mounter();

            try
            {
                Result rc = mount.Mount(FsClient, MountName, SpaceId, SaveDataId);
                if (rc.IsFailure()) return rc;

                Span<byte> rootPath = stackalloc byte[MaxPathLength];
                MakeRootPath(rootPath, MountName);

                rc = KvDatabase.Initialize(FsClient, new U8Span(rootPath), KvDatabaseCapacity, MemoryResource, BufferMemoryResource);
                if (rc.IsFailure()) return rc;

                IsInitialized = true;
                return Result.Success;
            }
            finally
            {
                mount.Dispose();
            }
        }

        /// <summary>
        /// Ensures that the database file exists and loads any existing entries.
        /// Does nothing if the database has already been loaded and <paramref name="forceLoad"/> is <see langword="true"/>.
        /// </summary>
        /// <param name="forceLoad">If <see langword="true"/>, forces the database to be reloaded,
        /// even it it was already loaded previously.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private Result TryLoadDatabase(bool forceLoad)
        {
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
                Result rc = mount.Mount(FsClient, MountName, SpaceId, SaveDataId);
                if (rc.IsFailure()) return rc;

                rc = KvDatabase.Load();
                if (rc.IsFailure()) return rc;

                bool createdNewFile = false;

                Span<byte> lastPublishedIdPath = stackalloc byte[MaxPathLength];
                MakeLastPublishedIdSaveFilePath(lastPublishedIdPath, MountName);

                try
                {
                    rc = FsClient.OpenFile(out FileHandle handle, new U8Span(lastPublishedIdPath), OpenMode.Read);

                    // Create the last published ID file if it doesn't exist.
                    if (rc.IsFailure())
                    {
                        if (!ResultFs.PathNotFound.Includes(rc)) return rc;

                        rc = FsClient.CreateFile(new U8Span(lastPublishedIdPath), LastPublishedIdFileSize);
                        if (rc.IsFailure()) return rc;

                        rc = FsClient.OpenFile(out handle, new U8Span(lastPublishedIdPath), OpenMode.Read);
                        if (rc.IsFailure()) return rc;

                        createdNewFile = true;

                        _lastPublishedId = 0;
                        IsKvdbLoaded = true;
                    }

                    try
                    {
                        // If we had to create the file earlier, we don't need to load the value again.
                        if (!createdNewFile)
                        {
                            rc = FsClient.ReadFile(handle, 0, SpanHelpers.AsByteSpan(ref _lastPublishedId));
                            if (rc.IsFailure()) return rc;

                            IsKvdbLoaded = true;
                        }

                        return Result.Success;
                    }
                    finally
                    {
                        FsClient.CloseFile(handle);
                    }
                }
                finally
                {
                    // The save data needs to be committed if we created the last published ID file.
                    if (createdNewFile)
                    {
                        // Note: Nintendo does not check this return value, probably because it's in a scope-exit block.
                        FsClient.Commit(MountName).IgnoreResult();
                    }
                }
            }
            finally
            {
                mount.Dispose();
            }
        }

        private void UpdateHandle() => Handle++;

        /// <summary>
        /// Adds a <see cref="Reader"/> to the list of registered readers.
        /// </summary>
        /// <param name="reader">The reader to add.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private Result RegisterReader(ReferenceCountedDisposable<Reader> reader)
        {
            OpenReaders.Add(new ReaderAccessor(reader));

            return Result.Success;
        }

        /// <summary>
        /// Removes any <see cref="Reader"/>s that are no longer in use from the registered readers.
        /// </summary>
        private void UnregisterReader()
        {
            int i = 0;
            List<ReaderAccessor> readers = OpenReaders;

            while (i < readers.Count)
            {
                // Remove the reader if there are no references to it. There is no need to increment
                // i in this case because the next reader in the list will be shifted to index i
                if (readers[i].IsExpired())
                {
                    readers.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        }

        /// <summary>
        /// Adjusts the position of any opened <see cref="Reader"/>s so that they still point to the
        /// same element after the addition or removal of another element. If the reader was on the
        /// element that was removed, it will now point to the element that was next in the list.
        /// </summary>
        /// <param name="key">The key of the element that was removed or added.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private Result FixReader(in SaveDataAttribute key)
        {
            foreach (ReaderAccessor accessor in OpenReaders)
            {
                using (ReferenceCountedDisposable<Reader> reader = accessor.Lock())
                {
                    reader?.Target.Fix(in key);
                }
            }

            return Result.Success;
        }

        /// <summary>
        /// Mounts the storage for a <see cref="SaveDataIndexer"/>, and unmounts the storage
        /// when the <see cref="Mounter"/> is disposed;
        /// </summary>
        private ref struct Mounter
        {
            private FileSystemClient FsClient { get; set; }
            private U8String MountName { get; set; }
            private bool IsMounted { get; set; }

            public Result Mount(FileSystemClient fsClient, U8String mountName, SaveDataSpaceId spaceId,
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
                        rc = FsClient.CreateSystemSaveData(spaceId, saveDataId, 0, SaveDataAvailableSize, SaveDataJournalSize, 0);
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

        private class ReaderAccessor
        {
            private ReferenceCountedDisposable<Reader>.WeakReference _reader;

            public ReaderAccessor(ReferenceCountedDisposable<Reader> reader)
            {
                _reader = new ReferenceCountedDisposable<Reader>.WeakReference(reader);
            }

            public ReferenceCountedDisposable<Reader> Lock()
            {
                return _reader.TryAddReference();
            }

            public bool IsExpired()
            {
                using (ReferenceCountedDisposable<Reader> reference = _reader.TryAddReference())
                {
                    return reference == null;
                }
            }
        }

        private class Reader : SaveDataInfoReaderImpl, ISaveDataInfoReader
        {
            private readonly SaveDataIndexer _indexer;
            private FlatMapKeyValueStore<SaveDataAttribute>.Iterator _iterator;
            private readonly int _handle;

            public Reader(SaveDataIndexer indexer)
            {
                _indexer = indexer;
                _handle = indexer.Handle;

                _iterator = indexer.GetBeginIterator();
            }

            public Result Read(out long readCount, OutBuffer saveDataInfoBuffer)
            {
                UnsafeHelpers.SkipParamInit(out readCount);

                lock (_indexer.Locker)
                {
                    // Indexer has been reloaded since this info reader was created
                    if (_handle != _indexer.Handle)
                    {
                        return ResultFs.InvalidHandle.Log();
                    }

                    Span<SaveDataInfo> outInfo = MemoryMarshal.Cast<byte, SaveDataInfo>(saveDataInfoBuffer.Buffer);

                    int i;
                    for (i = 0; !_iterator.IsEnd() && i < outInfo.Length; i++)
                    {
                        ref SaveDataAttribute key = ref _iterator.Get().Key;
                        ref SaveDataIndexerValue value = ref _iterator.GetValue<SaveDataIndexerValue>();

                        GenerateSaveDataInfo(out outInfo[i], in key, in value);

                        _iterator.Next();
                    }

                    readCount = i;
                    return Result.Success;
                }
            }

            public void Fix(in SaveDataAttribute attribute)
            {
                _indexer.FixIterator(ref _iterator, in attribute);
            }

            public void Dispose()
            {
                lock (_indexer.Locker)
                {
                    _indexer.UnregisterReader();
                }
            }
        }

        public void Dispose()
        {
            KvDatabase?.Dispose();
        }
    }
}
