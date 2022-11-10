using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Os;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Manages a list of cached save data file systems. Each file system is registered and retrieved
/// based on its save data ID and save data space ID.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class SaveDataFileSystemCacheManager : IDisposable
{
    [NonCopyable]
    private struct Cache
    {
        private SharedRef<ISaveDataFileSystem> _fileSystem;
        private ulong _saveDataId;
        private SaveDataSpaceId _spaceId;

        public void Dispose()
        {
            _fileSystem.Destroy();
        }

        public bool IsCached(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            return _fileSystem.HasValue && _spaceId == spaceId && _saveDataId == saveDataId;
        }

        public SharedRef<ISaveDataFileSystem> Move()
        {
            return SharedRef<ISaveDataFileSystem>.CreateMove(ref _fileSystem);
        }

        public void Register(ref SharedRef<ISaveDataFileSystem> fileSystem, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            _fileSystem.SetByMove(ref fileSystem);
            _spaceId = spaceId;
            _saveDataId = saveDataId;
        }

        public void Unregister()
        {
            _fileSystem.Reset();
        }
    }

    private SdkRecursiveMutexType _mutex;
    private Cache[] _cachedFileSystems;
    private int _maxCachedFileSystemCount;
    private int _nextCacheIndex;

    public SaveDataFileSystemCacheManager()
    {
        _mutex = new SdkRecursiveMutexType();
    }

    public void Dispose()
    {
        Cache[] caches = Shared.Move(ref _cachedFileSystems);

        if (caches is not null)
        {
            for (int i = 0; i < caches.Length; i++)
            {
                caches[i].Dispose();
            }
        }
    }

    public Result Initialize(int maxCacheCount)
    {
        Assert.SdkRequiresGreaterEqual(maxCacheCount, 0);

        using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        Assert.SdkAssert(_cachedFileSystems is null);

        _maxCachedFileSystemCount = maxCacheCount;
        if (maxCacheCount > 0)
        {
            // Note: The original checks for overflow here
            _cachedFileSystems = new Cache[maxCacheCount];
        }

        return Result.Success;
    }

    public UniqueLockRef<SdkRecursiveMutexType> GetScopedLock()
    {
        return new UniqueLockRef<SdkRecursiveMutexType>(ref _mutex);
    }

    public bool GetCache(ref SharedRef<ISaveDataFileSystem> outFileSystem, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Assert.SdkRequiresGreaterEqual(_maxCachedFileSystemCount, 0);

        using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        for (int i = 0; i < _maxCachedFileSystemCount; i++)
        {
            if (_cachedFileSystems[i].IsCached(spaceId, saveDataId))
            {
                using SharedRef<ISaveDataFileSystem> cachedFs = _cachedFileSystems[i].Move();
                outFileSystem.SetByMove(ref cachedFs.Ref());

                return true;
            }
        }

        return false;
    }

    public void Register(ref SharedRef<ISaveDataFileSystem> fileSystem, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Assert.SdkRequiresNotNull(in fileSystem);

        if (_maxCachedFileSystemCount <= 0)
            return;

        Assert.SdkRequiresGreaterEqual(_nextCacheIndex, 0);
        Assert.SdkRequiresGreater(_maxCachedFileSystemCount, _nextCacheIndex);

        if (!fileSystem.Get.IsSaveDataFileSystemCacheEnabled())
        {
            using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);
            fileSystem.Reset();
        }
        else if (spaceId == SaveDataSpaceId.SdSystem)
        {
            using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);
            fileSystem.Reset();
        }
        else
        {
            Result res = fileSystem.Get.RollbackOnlyModified();
            if (res.IsSuccess())
            {
                using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

                _cachedFileSystems[_nextCacheIndex].Register(ref fileSystem, spaceId, saveDataId);
                _nextCacheIndex = (_nextCacheIndex + 1) % _maxCachedFileSystemCount;
            }
        }
    }

    public void Unregister(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Assert.SdkRequiresGreaterEqual(_maxCachedFileSystemCount, 0);

        using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        for (int i = 0; i < _maxCachedFileSystemCount; i++)
        {
            if (_cachedFileSystems[i].IsCached(spaceId, saveDataId))
            {
                _cachedFileSystems[i].Unregister();
            }
        }
    }
}