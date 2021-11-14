using System;
using System.Collections.Generic;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Os;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Holds the <see cref="ISaveDataExtraDataAccessor"/>s for opened save data file systems.
/// </summary>
public class SaveDataExtraDataAccessorCacheManager : ISaveDataExtraDataAccessorCacheObserver
{
    [NonCopyable]
    private struct Cache : IDisposable
    {
        private WeakRef<ISaveDataExtraDataAccessor> _accessor;
        private readonly SaveDataSpaceId _spaceId;
        private readonly ulong _saveDataId;

        public Cache(in SharedRef<ISaveDataExtraDataAccessor> accessor, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            _accessor = new WeakRef<ISaveDataExtraDataAccessor>(in accessor);
            _spaceId = spaceId;
            _saveDataId = saveDataId;
        }

        public void Dispose()
        {
            _accessor.Destroy();
        }

        public bool Contains(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            return _spaceId == spaceId && _saveDataId == saveDataId;
        }

        public SharedRef<ISaveDataExtraDataAccessor> Lock()
        {
            return _accessor.Lock();
        }
    }

    private LinkedList<Cache> _accessorList;
    private SdkRecursiveMutexType _mutex;

    public SaveDataExtraDataAccessorCacheManager()
    {
        _accessorList = new LinkedList<Cache>();
        _mutex.Initialize();
    }

    public void Dispose()
    {
        using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        LinkedListNode<Cache> currentEntry = _accessorList.First;

        while (currentEntry is not null)
        {
            ref Cache entry = ref currentEntry.ValueRef;
            _accessorList.Remove(currentEntry);
            entry.Dispose();

            currentEntry = _accessorList.First;
        }

        _accessorList.Clear();
    }

    public Result Register(in SharedRef<ISaveDataExtraDataAccessor> accessor, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        var node = new LinkedListNode<Cache>(new Cache(in accessor, spaceId, saveDataId));

        using (ScopedLock.Lock(ref _mutex))
        {
            UnregisterImpl(spaceId, saveDataId);
            _accessorList.AddLast(node);
        }

        return Result.Success;
    }

    public void Unregister(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        UnregisterImpl(spaceId, saveDataId);
    }

    private void UnregisterImpl(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        LinkedListNode<Cache> currentNode = _accessorList.First;

        while (currentNode is not null)
        {
            if (currentNode.ValueRef.Contains(spaceId, saveDataId))
            {
                _accessorList.Remove(currentNode);
                return;
            }

            currentNode = currentNode.Next;
        }
    }

    public Result GetCache(ref SharedRef<ISaveDataExtraDataAccessor> outAccessor, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        LinkedListNode<Cache> currentNode = _accessorList.First;

        while (true)
        {
            if (currentNode is null)
                return ResultFs.TargetNotFound.Log();

            if (currentNode.ValueRef.Contains(spaceId, saveDataId))
                break;

            currentNode = currentNode.Next;
        }

        using SharedRef<ISaveDataExtraDataAccessor> accessor = currentNode.ValueRef.Lock();

        if (!accessor.HasValue)
            return ResultFs.TargetNotFound.Log();

        outAccessor.Reset(new SaveDataExtraDataResultConvertAccessor(ref accessor.Ref()));
        return Result.Success;
    }

    public UniqueLockRef<SdkRecursiveMutexType> GetScopedLock()
    {
        return new UniqueLockRef<SdkRecursiveMutexType>(ref _mutex);
    }
}
