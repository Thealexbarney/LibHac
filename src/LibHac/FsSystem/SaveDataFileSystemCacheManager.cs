using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Save;
using LibHac.Os;

namespace LibHac.FsSystem
{
    public class SaveDataFileSystemCacheManager : ISaveDataFileSystemCacheManager
    {
        private struct Cache
        {
            private ReferenceCountedDisposable<IFileSystem> _fileSystem;
            private ulong _saveDataId;
            private SaveDataSpaceId _spaceId;

            public void Dispose()
            {
                _fileSystem?.Dispose();
                _fileSystem = null;
            }

            public bool IsCached(SaveDataSpaceId spaceId, ulong saveDataId)
            {
                return _fileSystem is not null && _spaceId == spaceId && _saveDataId == saveDataId;
            }

            public ReferenceCountedDisposable<IFileSystem> Move()
            {
                return Shared.Move(ref _fileSystem);
            }

            // Note: Nintendo only supports caching SaveDataFileSystem. We support DirectorySaveDataFileSystem too,
            // so instead of calling methods on SaveDataFileSystem to get the save data info,
            // we pass them in as parameters.
            // Todo: Create a new interface for those methods?
            public void Register(ReferenceCountedDisposable<IFileSystem> fileSystem, SaveDataSpaceId spaceId,
                ulong saveDataId)
            {
                _spaceId = spaceId;
                _saveDataId = saveDataId;

                _fileSystem?.Dispose();
                _fileSystem = fileSystem;
            }

            public void Unregister()
            {
                _fileSystem?.Dispose();
                _fileSystem = null;
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
            Cache[] caches = _cachedFileSystems;

            for (int i = 0; i < caches.Length; i++)
            {
                caches[i].Dispose();
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

        public bool GetCache(out ReferenceCountedDisposable<IFileSystem> fileSystem, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            Assert.SdkRequiresGreaterEqual(_maxCachedFileSystemCount, 0);

            using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            for (int i = 0; i < _maxCachedFileSystemCount; i++)
            {
                if (_cachedFileSystems[i].IsCached(spaceId, saveDataId))
                {
                    fileSystem = _cachedFileSystems[i].Move();
                    return true;
                }
            }

            fileSystem = default;
            return false;
        }

        public void Register(ReferenceCountedDisposable<ApplicationTemporaryFileSystem> fileSystem)
        {
            throw new System.NotImplementedException();
        }

        public void Register(ReferenceCountedDisposable<SaveDataFileSystem> fileSystem)
        {
            throw new System.NotImplementedException();
        }

        public void Register(ReferenceCountedDisposable<DirectorySaveDataFileSystem> fileSystem)
        {
            if (_maxCachedFileSystemCount <= 0)
                return;

            Assert.SdkRequiresGreaterEqual(_nextCacheIndex, 0);
            Assert.SdkRequiresGreater(_maxCachedFileSystemCount, _nextCacheIndex);

            if (fileSystem.Target.GetSaveDataSpaceId() == SaveDataSpaceId.SdSystem)
                return;

            Result rc = fileSystem.Target.Rollback();
            if (rc.IsFailure()) return;

            using ScopedLock<SdkRecursiveMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            _cachedFileSystems[_nextCacheIndex].Register(fileSystem.AddReference<IFileSystem>(),
                fileSystem.Target.GetSaveDataSpaceId(), fileSystem.Target.GetSaveDataId());

            _nextCacheIndex = (_nextCacheIndex + 1) % _maxCachedFileSystemCount;
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

        public ScopedLock<SdkRecursiveMutexType> GetScopedLock()
        {
            return new ScopedLock<SdkRecursiveMutexType>(ref _mutex);
        }
    }
}