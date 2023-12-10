using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.Kvdb;
using LibHac.Os;
using LibHac.Sf;
using LibHac.Util;
using static LibHac.Fs.SaveData;

namespace LibHac.FsSrv;

/// <summary>
/// Indexes metadata for persistent save data stored on disk, holding key-value pairs of types
/// <see cref="SaveDataAttribute"/> and <see cref="SaveDataIndexerValue"/> respectively.
/// </summary>
/// <remarks>
/// Each <see cref="SaveDataIndexer"/> manages one to two save data spaces.
/// Each save data space is identified by a <see cref="SaveDataSpaceId"/>,
/// and has its own unique storage location on disk.
/// <para>Based on nnSdk 13.4.0 (FS 13.1.0)</para>
/// </remarks>
public class SaveDataIndexer : ISaveDataIndexer
{
    private const int KvDatabaseCapacity = 0x1080;
    private const int KvDatabaseReservedEntryCount = 0x80;

    private const int SaveDataAvailableSize = 0xC0000;
    private const int SaveDataJournalSize = 0xC0000;

    private const long LastPublishedIdFileSize = sizeof(long);
    private const int MaxPathLength = 0x30;

    private static ReadOnlySpan<byte> LastPublishedIdFileName => "lastPublishedId"u8;

    private static ReadOnlySpan<byte> MountDelimiter => ":/"u8;

    private delegate void SaveDataValueTransform(ref SaveDataIndexerValue value, ReadOnlySpan<byte> updateData);

    /// <summary>
    /// Mounts the storage for a <see cref="SaveDataIndexer"/>, and unmounts the storage
    /// when the <see cref="ScopedMount"/> is disposed;
    /// </summary>
    /// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
    [NonCopyableDisposable]
    private ref struct ScopedMount
    {
        private Array16<byte> _mountName;
        private bool _isMounted;

        // LibHac addition
        private FileSystemClient _fsClient;

        public ScopedMount(FileSystemClient fsClient)
        {
            _mountName = default;
            _isMounted = false;
            _fsClient = fsClient;
        }

        public void Dispose()
        {
            if (_isMounted)
            {
                _fsClient.Unmount(new U8Span(_mountName));
                _isMounted = false;
            }
        }

        public Result Mount(ReadOnlySpan<byte> mountName, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Assert.SdkRequires(!_isMounted);

            int mountNameLength = StringUtils.Strlcpy(_mountName, mountName, _mountName.Length);
            Assert.SdkLess(mountNameLength, _mountName.Length);

            _fsClient.DisableAutoSaveDataCreation();

            Result res = _fsClient.MountSystemSaveData(new U8Span(_mountName), spaceId, saveDataId);

            if (res.IsFailure())
            {
                if (ResultFs.TargetNotFound.Includes(res))
                {
                    res = _fsClient.CreateSystemSaveData(spaceId, saveDataId, 0, SaveDataAvailableSize,
                        SaveDataJournalSize, 0);
                    if (res.IsFailure()) return res.Miss();

                    res = _fsClient.MountSystemSaveData(new U8Span(_mountName), spaceId, saveDataId);
                    if (res.IsFailure()) return res.Miss();
                }
                else if (ResultFs.SignedSystemPartitionDataCorrupted.Includes(res))
                {
                    return res;
                }
                else if (ResultFs.DataCorrupted.Includes(res))
                {
                    if (spaceId == SaveDataSpaceId.SdSystem)
                        return res;

                    res = _fsClient.DeleteSaveData(spaceId, saveDataId);
                    if (res.IsFailure()) return res.Miss();

                    res = _fsClient.CreateSystemSaveData(spaceId, saveDataId, 0, 0xC0000, 0xC0000, 0);
                    if (res.IsFailure()) return res.Miss();

                    res = _fsClient.MountSystemSaveData(new U8Span(_mountName), spaceId, saveDataId);
                    if (res.IsFailure()) return res.Miss();
                }
                else
                {
                    return res;
                }
            }

            _isMounted = true;
            return Result.Success;
        }
    }

    /// <summary>
    /// Iterates through all the save data indexed in a <see cref="SaveDataIndexer"/>.
    /// </summary>
    /// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
    private class Reader : SaveDataInfoReaderImpl
    {
        private readonly SaveDataIndexer _indexer;
        private FlatMapKeyValueStore<SaveDataAttribute>.Iterator _iterator;
        private readonly int _handle;

        public Reader(SaveDataIndexer indexer)
        {
            _indexer = indexer;
            _iterator = indexer.GetBeginIterator();
            _handle = indexer.GetHandle();
        }

        public void Dispose()
        {
            _indexer.UnregisterReader();
        }

        public Result Read(out long readCount, OutBuffer saveDataInfoBuffer)
        {
            UnsafeHelpers.SkipParamInit(out readCount);

            using UniqueLockRef<SdkMutexType> scopedLock = _indexer.GetScopedLock();

            // Indexer has been reloaded since this info reader was created
            if (_handle != _indexer.GetHandle())
                return ResultFs.InvalidHandle.Log();

            Span<SaveDataInfo> outInfos = MemoryMarshal.Cast<byte, SaveDataInfo>(saveDataInfoBuffer.Buffer);

            int count;
            for (count = 0; !_iterator.IsEnd() && count < outInfos.Length; count++)
            {
                ref SaveDataAttribute key = ref _iterator.Get().Key;
                ref SaveDataIndexerValue value = ref _iterator.GetValue<SaveDataIndexerValue>();

                GenerateSaveDataInfo(out outInfos[count], in key, in value);

                _iterator.Next();
            }

            readCount = count;
            return Result.Success;
        }

        public void Fix(in SaveDataAttribute key)
        {
            if (_handle == _indexer.GetHandle())
                _indexer.FixIterator(ref _iterator, in key);
        }
    }

    private class ReaderAccessor : IDisposable
    {
        private WeakRef<Reader> _reader;

        public ReaderAccessor(ref readonly SharedRef<Reader> reader)
        {
            _reader = new WeakRef<Reader>(in reader);
        }

        public void Dispose() => _reader.Destroy();
        public bool IsExpired() => _reader.Expired;
        public SharedRef<Reader> Lock() => _reader.Lock();
    }

    private Array48<byte> _mountName;
    private ulong _indexerSaveDataId;
    private SaveDataSpaceId _spaceId;
    private MemoryResource _memoryResource;
    private MemoryResource _bufferMemoryResource;
    private FlatMapKeyValueStore<SaveDataAttribute> _kvDatabase;
    private SdkMutexType _mutex;
    private bool _isInitialized;
    private bool _isLoaded;
    private ulong _lastPublishedId;
    private int _handle;
    private LinkedList<ReaderAccessor> _openReaders;
    private bool _isDelayedReaderUnregistrationRequired;

    // LibHac addition
    private FileSystemClient _fsClient;

    public SaveDataIndexer(FileSystemClient fsClient, U8Span mountName, SaveDataSpaceId spaceId, ulong saveDataId,
        MemoryResource memoryResource)
    {
        _indexerSaveDataId = saveDataId;
        _spaceId = spaceId;
        _memoryResource = memoryResource;

        // Todo: FS uses a separate PooledBufferMemoryResource here
        _bufferMemoryResource = memoryResource;
        _kvDatabase = new FlatMapKeyValueStore<SaveDataAttribute>();
        _mutex = new SdkMutexType();
        _isInitialized = false;
        _isLoaded = false;
        _handle = 1;
        _openReaders = new LinkedList<ReaderAccessor>();
        _isDelayedReaderUnregistrationRequired = false;
        StringUtils.Copy(_mountName, mountName);

        _fsClient = fsClient;
    }

    public void Dispose()
    {
        Assert.SdkRequires(!_isDelayedReaderUnregistrationRequired);

        _kvDatabase?.Dispose();
    }

    private static void MakeLastPublishedIdSaveFilePath(Span<byte> buffer, ReadOnlySpan<byte> mountName)
    {
        // returns "%s:/%s", mountName, "lastPublishedId"
        var sb = new U8StringBuilder(buffer);
        sb.Append(mountName);
        sb.Append(MountDelimiter);
        sb.Append(LastPublishedIdFileName);

        Assert.SdkAssert(!sb.Overflowed);
    }

    private static void MakeRootPath(Span<byte> buffer, ReadOnlySpan<byte> mountName)
    {
        // returns "%s:/", mountName
        var sb = new U8StringBuilder(buffer);
        sb.Append(mountName);
        sb.Append(MountDelimiter);

        Assert.SdkAssert(!sb.Overflowed);
    }

    /// <summary>
    /// Generates a <see cref="SaveDataInfo"/> from the provided <see cref="SaveDataAttribute"/> and <see cref="SaveDataIndexerValue"/>.
    /// </summary>
    /// <param name="info">When this method returns, contains the generated <see cref="SaveDataInfo"/>.</param>
    /// <param name="key">The key used to generate the <see cref="SaveDataInfo"/>.</param>
    /// <param name="value">The value used to generate the <see cref="SaveDataInfo"/>.</param>
    public static void GenerateSaveDataInfo(out SaveDataInfo info, in SaveDataAttribute key,
        in SaveDataIndexerValue value)
    {
        info = new SaveDataInfo
        {
            SaveDataId = value.SaveDataId,
            SpaceId = value.SpaceId,
            Size = value.Size,
            State = value.State,
            StaticSaveDataId = key.StaticSaveDataId,
            ProgramId = key.ProgramId,
            Type = key.Type,
            UserId = key.UserId,
            Index = key.Index,
            Rank = key.Rank
        };
    }

    public UniqueLockRef<SdkMutexType> GetScopedLock()
    {
        return new UniqueLockRef<SdkMutexType>(ref _mutex);
    }

    /// <summary>
    /// Initializes <see cref="_kvDatabase"/> and ensures that the indexer's save data is created.
    /// Does nothing if this <see cref="SaveDataIndexer"/> has already been initialized.
    /// </summary>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    private Result TryInitializeDatabase()
    {
        if (_isInitialized)
            return Result.Success;

        using var scopedMount = new ScopedMount(_fsClient);

        Result res = scopedMount.Mount(_mountName, _spaceId, _indexerSaveDataId);
        if (res.IsFailure()) return res.Miss();

        Span<byte> rootPath = stackalloc byte[MaxPathLength];
        MakeRootPath(rootPath, _mountName);

        res = _kvDatabase.Initialize(_fsClient, new U8Span(rootPath), KvDatabaseCapacity, _memoryResource, _bufferMemoryResource);
        if (res.IsFailure()) return res.Miss();

        _isInitialized = true;
        return Result.Success;
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
            _isLoaded = false;

        if (_isLoaded)
            return Result.Success;

        using var scopedMount = new ScopedMount(_fsClient);
        Result res = scopedMount.Mount(_mountName, _spaceId, _indexerSaveDataId);
        if (res.IsFailure()) return res.Miss();

        res = _kvDatabase.Load();
        if (res.IsFailure()) return res.Miss();

        bool createdNewFile = false;

        Span<byte> lastPublishedIdPath = stackalloc byte[MaxPathLength];
        MakeLastPublishedIdSaveFilePath(lastPublishedIdPath, _mountName);

        try
        {
            res = _fsClient.OpenFile(out FileHandle file, new U8Span(lastPublishedIdPath), OpenMode.Read);

            // Create the last published ID file if it doesn't exist.
            if (res.IsFailure())
            {
                if (!ResultFs.PathNotFound.Includes(res)) return res;

                res = _fsClient.CreateFile(new U8Span(lastPublishedIdPath), LastPublishedIdFileSize);
                if (res.IsFailure()) return res.Miss();

                res = _fsClient.OpenFile(out file, new U8Span(lastPublishedIdPath), OpenMode.Read);
                if (res.IsFailure()) return res.Miss();

                createdNewFile = true;
            }

            try
            {
                // If we had to create the file earlier, we don't need to load the value again.
                if (!createdNewFile)
                {
                    res = _fsClient.ReadFile(file, 0, SpanHelpers.AsByteSpan(ref _lastPublishedId));
                    if (res.IsFailure()) return res.Miss();
                }
                else
                {
                    _lastPublishedId = 0;
                }

                _isLoaded = true;
                return Result.Success;
            }
            finally
            {
                _fsClient.CloseFile(file);
            }
        }
        finally
        {
            // The save data needs to be committed if we created the last published ID file.
            if (createdNewFile)
            {
                // Nintendo does not check this return value.
                _fsClient.CommitSaveData(new U8Span(_mountName)).IgnoreResult();
            }
        }
    }

    public Result Publish(out ulong saveDataId, in SaveDataAttribute key)
    {
        UnsafeHelpers.SkipParamInit(out saveDataId);

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        Result res = TryInitializeDatabase();
        if (res.IsFailure()) return res.Miss();

        res = TryLoadDatabase(forceLoad: false);
        if (res.IsFailure()) return res.Miss();

        Assert.SdkRequires(_isLoaded);

        // Make sure the key isn't in the database already.
        SaveDataIndexerValue value = default;
        res = _kvDatabase.Get(out _, in key, SpanHelpers.AsByteSpan(ref value));

        if (res.IsSuccess())
        {
            return ResultFs.AlreadyExists.Log();
        }

        // Get the next save data ID and write the new key/value to the database
        _lastPublishedId++;
        ulong newSaveDataId = _lastPublishedId;

        value = new SaveDataIndexerValue { SaveDataId = newSaveDataId };
        res = _kvDatabase.Set(in key, SpanHelpers.AsByteSpan(ref value));

        if (res.IsFailure())
        {
            _lastPublishedId--;
            return res;
        }

        res = FixReader(in key);
        if (res.IsFailure()) return res.Miss();

        saveDataId = value.SaveDataId;
        return Result.Success;
    }

    public Result PutStaticSaveDataIdIndex(in SaveDataAttribute key)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        Result res = TryInitializeDatabase();
        if (res.IsFailure()) return res.Miss();

        res = TryLoadDatabase(forceLoad: false);
        if (res.IsFailure()) return res.Miss();

        Assert.SdkRequires(_isLoaded);
        Assert.SdkRequires(key.StaticSaveDataId != InvalidSystemSaveDataId);
        Assert.SdkRequires(key.UserId == InvalidUserId);

        // Iterate through all existing values to check if the save ID is already in use.
        FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator = _kvDatabase.GetBeginIterator();
        while (!iterator.IsEnd())
        {
            if (iterator.GetValue<SaveDataIndexerValue>().SaveDataId == key.StaticSaveDataId)
            {
                return ResultFs.AlreadyExists.Log();
            }

            iterator.Next();
        }

        var value = new SaveDataIndexerValue { SaveDataId = key.StaticSaveDataId };

        res = _kvDatabase.Set(in key, SpanHelpers.AsReadOnlyByteSpan(in value));
        if (res.IsFailure()) return res.Miss();

        res = FixReader(in key);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public bool IsRemainedReservedOnly()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        return _kvDatabase.Count >= KvDatabaseCapacity - KvDatabaseReservedEntryCount;
    }

    public Result Delete(ulong saveDataId)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        Result res = TryInitializeDatabase();
        if (res.IsFailure()) return res.Miss();

        res = TryLoadDatabase(forceLoad: false);
        if (res.IsFailure()) return res.Miss();

        Assert.SdkRequires(_isLoaded);

        FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator = _kvDatabase.GetBeginIterator();

        while (true)
        {
            if (iterator.IsEnd())
                return ResultFs.TargetNotFound.Log();

            if (iterator.GetValue<SaveDataIndexerValue>().SaveDataId == saveDataId)
                break;

            iterator.Next();
        }

        SaveDataAttribute key = iterator.Get().Key;

        res = _kvDatabase.Delete(in key);
        if (res.IsFailure()) return res.Miss();

        res = FixReader(in key);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result Commit()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        // Make sure we've loaded the database
        Result res = TryInitializeDatabase();
        if (res.IsFailure()) return res.Miss();

        res = TryLoadDatabase(forceLoad: false);
        if (res.IsFailure()) return res.Miss();

        Assert.SdkRequires(_isLoaded);

        // Mount the indexer's save data
        using var scopedMount = new ScopedMount(_fsClient);
        res = scopedMount.Mount(_mountName, _spaceId, _indexerSaveDataId);
        if (res.IsFailure()) return res.Miss();

        // Save the actual database
        res = _kvDatabase.Save();
        if (res.IsFailure()) return res.Miss();

        // Save the last published save data ID
        Span<byte> lastPublishedIdPath = stackalloc byte[MaxPathLength];
        MakeLastPublishedIdSaveFilePath(lastPublishedIdPath, _mountName);

        res = _fsClient.OpenFile(out FileHandle file, new U8Span(lastPublishedIdPath), OpenMode.Write);
        if (res.IsFailure()) return res.Miss();

        bool isFileClosed = false;

        try
        {
            res = _fsClient.WriteFile(file, 0, SpanHelpers.AsByteSpan(ref _lastPublishedId), WriteOption.None);
            if (res.IsFailure()) return res.Miss();

            res = _fsClient.FlushFile(file);
            if (res.IsFailure()) return res.Miss();

            _fsClient.CloseFile(file);
            isFileClosed = true;

            return _fsClient.CommitSaveData(new U8Span(_mountName));
        }
        finally
        {
            if (!isFileClosed)
            {
                _fsClient.CloseFile(file);
            }
        }
    }

    public Result Rollback()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        Result res = TryInitializeDatabase();
        if (res.IsFailure()) return res.Miss();

        res = TryLoadDatabase(forceLoad: true);
        if (res.IsFailure()) return res.Miss();

        UpdateHandle();
        return Result.Success;
    }

    public Result Reset()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_isLoaded)
            _isLoaded = false;

        Result res = _fsClient.DeleteSaveData(_indexerSaveDataId);

        if (res.IsSuccess() || ResultFs.TargetNotFound.Includes(res))
        {
            UpdateHandle();
        }

        return res;
    }

    public Result Get(out SaveDataIndexerValue value, in SaveDataAttribute key)
    {
        UnsafeHelpers.SkipParamInit(out value);

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        Result res = TryInitializeDatabase();
        if (res.IsFailure()) return res.Miss();

        res = TryLoadDatabase(forceLoad: false);
        if (res.IsFailure()) return res.Miss();

        Assert.SdkRequires(_isLoaded);

        res = _kvDatabase.Get(out _, in key, SpanHelpers.AsByteSpan(ref value));

        if (res.IsFailure())
        {
            return ResultFs.TargetNotFound.LogConverted(res);
        }

        return Result.Success;
    }

    public Result SetSpaceId(ulong saveDataId, SaveDataSpaceId spaceId)
    {
        return UpdateValueBySaveDataId(saveDataId,
            static (ref SaveDataIndexerValue value, ReadOnlySpan<byte> data) =>
            {
                value.SpaceId = (SaveDataSpaceId)data[0];
            },
            SpanHelpers.AsReadOnlyByteSpan(in spaceId));
    }

    public Result SetSize(ulong saveDataId, long size)
    {
        return UpdateValueBySaveDataId(saveDataId,
            static (ref SaveDataIndexerValue value, ReadOnlySpan<byte> data) =>
            {
                value.Size = BinaryPrimitives.ReadInt64LittleEndian(data);
            },
            SpanHelpers.AsReadOnlyByteSpan(in size));
    }

    public Result SetState(ulong saveDataId, SaveDataState state)
    {
        return UpdateValueBySaveDataId(saveDataId,
            static (ref SaveDataIndexerValue value, ReadOnlySpan<byte> data) =>
            {
                value.State = (SaveDataState)data[0];
            },
            SpanHelpers.AsReadOnlyByteSpan(in state));
    }

    public Result GetKey(out SaveDataAttribute key, ulong saveDataId)
    {
        UnsafeHelpers.SkipParamInit(out key);

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        Result res = TryInitializeDatabase();
        if (res.IsFailure()) return res.Miss();

        res = TryLoadDatabase(forceLoad: false);
        if (res.IsFailure()) return res.Miss();

        Assert.SdkRequires(_isLoaded);

        FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator = _kvDatabase.GetBeginIterator();

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

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        Result res = TryInitializeDatabase();
        if (res.IsFailure()) return res.Miss();

        res = TryLoadDatabase(forceLoad: false);
        if (res.IsFailure()) return res.Miss();

        Assert.SdkRequires(_isLoaded);

        FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator = _kvDatabase.GetBeginIterator();

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
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        Result res = TryInitializeDatabase();
        if (res.IsFailure()) return res.Miss();

        res = TryLoadDatabase(forceLoad: false);
        if (res.IsFailure()) return res.Miss();

        Assert.SdkRequires(_isLoaded);

        FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator = _kvDatabase.GetLowerBoundIterator(in key);

        // Key was not found
        if (iterator.IsEnd())
            return ResultFs.TargetNotFound.Log();

        iterator.GetValue<SaveDataIndexerValue>() = value;
        return Result.Success;
    }

    public int GetIndexCount()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        return _kvDatabase.Count;
    }

    /// <summary>
    /// Adds a <see cref="Reader"/> to the list of registered readers.
    /// </summary>
    /// <param name="reader">The reader to add.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    private Result RegisterReader(ref readonly SharedRef<Reader> reader)
    {
        Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

        _openReaders.AddLast(new ReaderAccessor(in reader));

        return Result.Success;
    }

    public void UnregisterReader()
    {
        if (_mutex.IsLockedByCurrentThread())
        {
            _isDelayedReaderUnregistrationRequired = true;
        }
        else
        {
            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);
            UnregisterReaderImpl();
        }
    }

    /// <summary>
    /// Removes any <see cref="Reader"/>s that are no longer in use from the registered readers.
    /// </summary>
    private void UnregisterReaderImpl()
    {
        Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

        _isDelayedReaderUnregistrationRequired = false;

        LinkedListNode<ReaderAccessor> node = _openReaders.First;

        while (node is not null)
        {
            // Grab the next node so we can continue iterating if we need to delete the current node
            LinkedListNode<ReaderAccessor> currentNode = node;
            node = node.Next;

            if (currentNode.Value.IsExpired())
            {
                _openReaders.Remove(currentNode);
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
        Assert.SdkRequires(_mutex.IsLockedByCurrentThread());
        Assert.SdkRequires(!_isDelayedReaderUnregistrationRequired);

        foreach (ReaderAccessor accessor in _openReaders)
        {
            using SharedRef<Reader> reader = accessor.Lock();

            if (reader.HasValue)
            {
                reader.Get.Fix(in key);
            }
        }

        if (_isDelayedReaderUnregistrationRequired)
        {
            UnregisterReaderImpl();
            Assert.SdkRequires(!_isDelayedReaderUnregistrationRequired);
        }

        return Result.Success;
    }

    public Result OpenSaveDataInfoReader(ref SharedRef<SaveDataInfoReaderImpl> outInfoReader)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        Result res = TryInitializeDatabase();
        if (res.IsFailure()) return res.Miss();

        res = TryLoadDatabase(forceLoad: false);
        if (res.IsFailure()) return res.Miss();

        // Create the reader and register it in the opened-reader list
        using var reader = new SharedRef<Reader>(new Reader(this));
        res = RegisterReader(in reader);
        if (res.IsFailure()) return res.Miss();

        outInfoReader.SetByMove(ref reader.Ref);
        return Result.Success;
    }

    private void FixIterator(ref FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator, in SaveDataAttribute key)
    {
        Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

        _kvDatabase.FixIterator(ref iterator, in key);
    }

    private FlatMapKeyValueStore<SaveDataAttribute>.Iterator GetBeginIterator()
    {
        Assert.SdkRequires(_isLoaded);
        Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

        return _kvDatabase.GetBeginIterator();
    }

    public int GetHandle()
    {
        Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

        return _handle;
    }

    private void UpdateHandle()
    {
        Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

        _handle++;
    }

    private Result UpdateValueBySaveDataId(ulong saveDataId, SaveDataValueTransform func, ReadOnlySpan<byte> data)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        Result res = TryInitializeDatabase();
        if (res.IsFailure()) return res.Miss();

        res = TryLoadDatabase(forceLoad: false);
        if (res.IsFailure()) return res.Miss();

        Assert.SdkRequires(_isLoaded);

        SaveDataIndexerValue value;
        FlatMapKeyValueStore<SaveDataAttribute>.Iterator iterator = _kvDatabase.GetBeginIterator();

        // Find the save with the specified ID
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

        // Run the function on the save data's indexer value and update the value in the database
        func(ref value, data);

        res = _kvDatabase.Set(in iterator.Get().Key, SpanHelpers.AsReadOnlyByteSpan(in value));
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}