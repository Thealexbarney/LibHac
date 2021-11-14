using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Os;

namespace LibHac.FsSystem;

public class SaveDataFileSystemCacheManager : ISaveDataFileSystemCacheManager
{
    [NonCopyable]
    private struct Cache
    {
        // Note: Nintendo only supports caching SaveDataFileSystem. We support DirectorySaveDataFileSystem too,
        // so we use a wrapper class to simplify the logic here.
        private SharedRef<SaveDataFileSystemHolder> _fileSystem;
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

        public void Move(ref SharedRef<SaveDataFileSystemHolder> outFileSystem)
        {
            outFileSystem.SetByMove(ref _fileSystem);
        }

        public void Register(ref SharedRef<SaveDataFileSystemHolder> fileSystem)
        {
            _spaceId = fileSystem.Get.GetSaveDataSpaceId();
            _saveDataId = fileSystem.Get.GetSaveDataId();

            _fileSystem.SetByMove(ref fileSystem);
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
        _mutex.Initialize();
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
        if (maxCacheCount <= 0)
            return Result.Success;

        // Note: The original checks for overflow here
        _cachedFileSystems = new Cache[maxCacheCount];
        return Result.Success;
    }

    public bool GetCache(ref SharedRef<SaveDataFileSystemHolder> outFileSystem, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        Assert.SdkRequiresGreaterEqual(_maxCachedFileSystemCount, 0);

        using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        for (int i = 0; i < _maxCachedFileSystemCount; i++)
        {
            if (_cachedFileSystems[i].IsCached(spaceId, saveDataId))
            {
                _cachedFileSystems[i].Move(ref outFileSystem);
                return true;
            }
        }

        return false;
    }

    public void Register(ref SharedRef<ApplicationTemporaryFileSystem> fileSystem)
    {
        // Don't cache temporary save data
        using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);
        fileSystem.Reset();
    }

    public void Register(ref SharedRef<SaveDataFileSystemHolder> fileSystem)
    {
        if (_maxCachedFileSystemCount <= 0)
            return;

        Assert.SdkRequiresGreaterEqual(_nextCacheIndex, 0);
        Assert.SdkRequiresGreater(_maxCachedFileSystemCount, _nextCacheIndex);

        if (fileSystem.Get.GetSaveDataSpaceId() == SaveDataSpaceId.SdSystem)
        {
            // Don't cache system save data
            using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);
            fileSystem.Reset();
        }
        else
        {
            Result rc = fileSystem.Get.RollbackOnlyModified();
            if (rc.IsFailure()) return;

            using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            _cachedFileSystems[_nextCacheIndex].Register(ref fileSystem);
            _nextCacheIndex = (_nextCacheIndex + 1) % _maxCachedFileSystemCount;
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

    public UniqueLockRef<SdkRecursiveMutexType> GetScopedLock()
    {
        return new UniqueLockRef<SdkRecursiveMutexType>(ref _mutex);
    }
}
