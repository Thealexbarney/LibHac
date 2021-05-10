using System.Collections.Generic;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Os;

namespace LibHac.FsSrv.Impl
{
    /// <summary>
    /// Holds the <see cref="ISaveDataExtraDataAccessor"/>s for opened save data file systems.
    /// </summary>
    public class SaveDataExtraDataAccessorCacheManager : ISaveDataExtraDataAccessorCacheObserver
    {
        private struct Cache
        {
            private ReferenceCountedDisposable<ISaveDataExtraDataAccessor>.WeakReference _accessor;
            private readonly SaveDataSpaceId _spaceId;
            private readonly ulong _saveDataId;

            public Cache(ReferenceCountedDisposable<ISaveDataExtraDataAccessor> accessor, SaveDataSpaceId spaceId,
                ulong saveDataId)
            {
                _accessor = new ReferenceCountedDisposable<ISaveDataExtraDataAccessor>.WeakReference(accessor);
                _spaceId = spaceId;
                _saveDataId = saveDataId;
            }

            public bool Contains(SaveDataSpaceId spaceId, ulong saveDataId)
            {
                return _spaceId == spaceId && _saveDataId == saveDataId;
            }

            public ReferenceCountedDisposable<ISaveDataExtraDataAccessor> Lock()
            {
                return _accessor.TryAddReference();
            }
        }

        private readonly LinkedList<Cache> _accessorList;
        private SdkRecursiveMutexType _mutex;

        public SaveDataExtraDataAccessorCacheManager()
        {
            _accessorList = new LinkedList<Cache>();
            _mutex.Initialize();
        }

        public void Dispose()
        {
            _accessorList.Clear();
        }

        public Result Register(ReferenceCountedDisposable<ISaveDataExtraDataAccessor> accessor,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            var cache = new Cache(accessor, spaceId, saveDataId);

            using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            _accessorList.AddLast(cache);
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

        public Result GetCache(out ReferenceCountedDisposable<ISaveDataExtraDataAccessor> accessor,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out accessor);

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

            ReferenceCountedDisposable<ISaveDataExtraDataAccessor> tempAccessor = null;
            try
            {
                tempAccessor = currentNode.ValueRef.Lock();

                // Return early if the accessor was already disposed
                if (tempAccessor is null)
                {
                    // Note: Nintendo doesn't remove the accessor from the list in this case
                    _accessorList.Remove(currentNode);
                    return ResultFs.TargetNotFound.Log();
                }

                accessor = SaveDataExtraDataResultConvertAccessor.CreateShared(ref tempAccessor);
                return Result.Success;
            }
            finally
            {
                tempAccessor?.Dispose();
            }
        }

        public ScopedLock<SdkRecursiveMutexType> GetScopedLock()
        {
            return new ScopedLock<SdkRecursiveMutexType>(ref _mutex);
        }
    }
}