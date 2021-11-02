using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv;
using LibHac.Os;

namespace LibHac.FsSystem
{
    internal struct DirectorySaveDataFileSystemGlobals
    {
        public SdkMutexType SynchronizeDirectoryMutex;

        public void Initialize(FileSystemClient fsClient)
        {
            SynchronizeDirectoryMutex.Initialize();
        }
    }

    /// <summary>
    /// An <see cref="IFileSystem"/> that provides transactional commits for savedata on top of another base IFileSystem.
    /// </summary>
    /// <remarks>
    /// Transactional commits should be atomic as long as the <see cref="IFileSystem.RenameDirectory"/> function of the
    /// underlying <see cref="IFileSystem"/> is atomic.
    /// <para>Based on FS 12.1.0 (nnSdk 12.3.1)</para>
    /// </remarks>
    public class DirectorySaveDataFileSystem : IFileSystem, ISaveDataExtraDataAccessor
    {
        private const int IdealWorkBufferSize = 0x100000; // 1 MiB

        private static ReadOnlySpan<byte> CommittedDirectoryName => new[] { (byte)'/', (byte)'0' };
        private static ReadOnlySpan<byte> ModifiedDirectoryName => new[] { (byte)'/', (byte)'1' };
        private static ReadOnlySpan<byte> SynchronizingDirectoryName => new[] { (byte)'/', (byte)'_' };
        private static ReadOnlySpan<byte> LockFileName => new[] { (byte)'/', (byte)'.', (byte)'l', (byte)'o', (byte)'c', (byte)'k' };

        private FileSystemClient _fsClient;
        private IFileSystem _baseFs;
        private SdkMutexType _mutex;
        private UniqueRef<IFileSystem> _uniqueBaseFs;

        private int _openWritableFileCount;
        private bool _isJournalingSupported;
        private bool _isMultiCommitSupported;
        private bool _isJournalingEnabled;

        // Additions to support extra data
        private ISaveDataCommitTimeStampGetter _timeStampGetter;
        private RandomDataGenerator _randomGenerator;

        // Additions to support caching
        private ISaveDataExtraDataAccessorCacheObserver _cacheObserver;
        private SaveDataSpaceId _spaceId;
        private ulong _saveDataId;

        // Additions to ensure only one directory save data fs is opened at a time
        private UniqueRef<IFile> _lockFile;

        private class DirectorySaveDataFile : IFile
        {
            private UniqueRef<IFile> _baseFile;
            private DirectorySaveDataFileSystem _parentFs;
            private OpenMode _mode;

            public DirectorySaveDataFile(ref UniqueRef<IFile> baseFile, DirectorySaveDataFileSystem parentFs, OpenMode mode)
            {
                _baseFile = new UniqueRef<IFile>(ref baseFile);
                _parentFs = parentFs;
                _mode = mode;
            }

            public override void Dispose()
            {
                _baseFile.Destroy();

                if (_mode.HasFlag(OpenMode.Write))
                {
                    _parentFs.DecrementWriteOpenFileCount();
                    _mode = default;
                }

                base.Dispose();
            }

            protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
                in ReadOption option)
            {
                return _baseFile.Get.Read(out bytesRead, offset, destination, in option);
            }

            protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
            {
                return _baseFile.Get.Write(offset, source, in option);
            }

            protected override Result DoFlush()
            {
                return _baseFile.Get.Flush();
            }

            protected override Result DoGetSize(out long size)
            {
                return _baseFile.Get.GetSize(out size);
            }

            protected override Result DoSetSize(long size)
            {
                return _baseFile.Get.SetSize(size);
            }

            protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset,
                long size, ReadOnlySpan<byte> inBuffer)
            {
                return _baseFile.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
            }
        }

        /// <summary>
        /// Create an uninitialized <see cref="DirectorySaveDataFileSystem"/>.
        /// </summary>
        /// <param name="baseFileSystem">The base <see cref="IFileSystem"/> to use.</param>
        public DirectorySaveDataFileSystem(IFileSystem baseFileSystem)
        {
            _baseFs = baseFileSystem;
            _mutex.Initialize();
        }

        /// <summary>
        /// Create an uninitialized <see cref="DirectorySaveDataFileSystem"/>.
        /// </summary>
        /// <param name="baseFileSystem">The base <see cref="IFileSystem"/> to use.</param>
        public DirectorySaveDataFileSystem(ref UniqueRef<IFileSystem> baseFileSystem)
        {
            _baseFs = baseFileSystem.Get;
            _mutex.Initialize();
            _uniqueBaseFs = new UniqueRef<IFileSystem>(ref baseFileSystem);
        }

        /// <summary>
        /// Create an uninitialized <see cref="DirectorySaveDataFileSystem"/>.
        /// If a <see cref="FileSystemClient"/> is provided a global mutex will be used when synchronizing directories.
        /// Running outside of a Horizon context doesn't require this mutex,
        /// and null can be passed to <paramref name="fsClient"/>.
        /// </summary>
        /// <param name="baseFileSystem">The base <see cref="IFileSystem"/> to use.</param>
        /// <param name="fsClient">The <see cref="FileSystemClient"/> to use. May be null.</param>
        public DirectorySaveDataFileSystem(IFileSystem baseFileSystem, FileSystemClient fsClient)
        {
            _baseFs = baseFileSystem;
            _mutex.Initialize();
            _fsClient = fsClient;
        }

        /// <summary>
        /// Create an uninitialized <see cref="DirectorySaveDataFileSystem"/>.
        /// If a <see cref="FileSystemClient"/> is provided a global mutex will be used when synchronizing directories.
        /// Running outside of a Horizon context doesn't require this mutex,
        /// and null can be passed to <paramref name="fsClient"/>.
        /// </summary>
        /// <param name="baseFileSystem">The base <see cref="IFileSystem"/> to use.</param>
        /// <param name="fsClient">The <see cref="FileSystemClient"/> to use. May be null.</param>
        public DirectorySaveDataFileSystem(ref UniqueRef<IFileSystem> baseFileSystem, FileSystemClient fsClient)
        {
            _baseFs = baseFileSystem.Get;
            _mutex.Initialize();
            _uniqueBaseFs = new UniqueRef<IFileSystem>(ref baseFileSystem);
            _fsClient = fsClient;
        }

        public override void Dispose()
        {
            _lockFile.Destroy();

            _cacheObserver?.Unregister(_spaceId, _saveDataId);
            _uniqueBaseFs.Destroy();
            base.Dispose();
        }

        [NonCopyable]
        private ref struct RetryClosure
        {
            public DirectorySaveDataFileSystem This;
            public Path CommittedPath;
            public Path ModifiedPath;
            public Path SynchronizingPath;

            public void Dispose()
            {
                CommittedPath.Dispose();
                ModifiedPath.Dispose();
                SynchronizingPath.Dispose();
            }
        }

        private delegate Result RetryDelegate(in RetryClosure closure);

        private Result RetryFinitelyForTargetLocked(RetryDelegate function, in RetryClosure closure)
        {
            const int maxRetryCount = 10;
            const int retryWaitTimeMs = 100;

            int remainingRetries = maxRetryCount;

            while (true)
            {
                Result rc = function(in closure);

                if (rc.IsSuccess())
                    return rc;

                if (!ResultFs.TargetLocked.Includes(rc))
                    return rc;

                if (remainingRetries <= 0)
                    return rc;

                remainingRetries--;

                if (_fsClient is not null)
                {
                    _fsClient.Hos.Os.SleepThread(TimeSpan.FromMilliSeconds(retryWaitTimeMs));
                }
                else
                {
                    System.Threading.Thread.Sleep(retryWaitTimeMs);
                }
            }
        }

        public Result Initialize(bool isJournalingSupported, bool isMultiCommitSupported, bool isJournalingEnabled)
        {
            return Initialize(null, null, isJournalingSupported, isMultiCommitSupported, isJournalingEnabled);
        }

        public Result Initialize(ISaveDataCommitTimeStampGetter timeStampGetter, RandomDataGenerator randomGenerator,
            bool isJournalingSupported, bool isMultiCommitSupported, bool isJournalingEnabled)
        {
            _isJournalingSupported = isJournalingSupported;
            _isMultiCommitSupported = isMultiCommitSupported;
            _isJournalingEnabled = isJournalingEnabled;
            _timeStampGetter = timeStampGetter ?? _timeStampGetter;
            _randomGenerator = randomGenerator ?? _randomGenerator;

            // Open the lock file
            Result rc = GetFileSystemLock();
            if (rc.IsFailure()) return rc;

            using var pathModifiedDirectory = new Path();
            rc = PathFunctions.SetUpFixedPath(ref pathModifiedDirectory.Ref(), ModifiedDirectoryName);
            if (rc.IsFailure()) return rc;

            using var pathCommittedDirectory = new Path();
            rc = PathFunctions.SetUpFixedPath(ref pathCommittedDirectory.Ref(), CommittedDirectoryName);
            if (rc.IsFailure()) return rc;

            using var pathSynchronizingDirectory = new Path();
            rc = PathFunctions.SetUpFixedPath(ref pathSynchronizingDirectory.Ref(), SynchronizingDirectoryName);
            if (rc.IsFailure()) return rc;

            // Ensure the working directory exists
            rc = _baseFs.GetEntryType(out _, in pathModifiedDirectory);

            if (rc.IsFailure())
            {
                if (!ResultFs.PathNotFound.Includes(rc))
                    return rc;

                rc = _baseFs.CreateDirectory(in pathModifiedDirectory);
                if (rc.IsFailure()) return rc;

                if (_isJournalingSupported)
                {
                    rc = _baseFs.CreateDirectory(in pathCommittedDirectory);

                    // Nintendo returns on all failures, but we'll keep going if committed already exists
                    // to avoid confusing people manually creating savedata in emulators
                    if (rc.IsFailure() && !ResultFs.PathAlreadyExists.Includes(rc))
                        return rc;
                }
            }

            // Only the working directory is needed for non-journaling savedata
            if (_isJournalingSupported)
            {
                rc = _baseFs.GetEntryType(out _, in pathCommittedDirectory);

                if (rc.IsSuccess())
                {
                    // The previous commit successfully completed. Copy the committed dir to the working dir.
                    if (_isJournalingEnabled)
                    {
                        rc = SynchronizeDirectory(in pathModifiedDirectory, in pathCommittedDirectory);
                        if (rc.IsFailure()) return rc;
                    }
                }
                else if (ResultFs.PathNotFound.Includes(rc))
                {
                    // If a previous commit failed, the committed dir may be missing.
                    // Finish that commit by copying the working dir to the committed dir
                    rc = SynchronizeDirectory(in pathSynchronizingDirectory, in pathModifiedDirectory);
                    if (rc.IsFailure()) return rc;

                    rc = _baseFs.RenameDirectory(in pathSynchronizingDirectory, in pathCommittedDirectory);
                    if (rc.IsFailure()) return rc;
                }
                else
                {
                    return rc;
                }
            }

            rc = InitializeExtraData();
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        private Result GetFileSystemLock()
        {
            // Having an open lock file means we already have the lock for the file system.
            if (_lockFile.HasValue)
                return Result.Success;

            using var pathLockFile = new Path();
            Result rc = PathFunctions.SetUpFixedPath(ref pathLockFile.Ref(), LockFileName);
            if (rc.IsFailure()) return rc;

            rc = _baseFs.OpenFile(ref _lockFile, in pathLockFile, OpenMode.ReadWrite);

            if (rc.IsFailure())
            {
                if (ResultFs.PathNotFound.Includes(rc))
                {
                    rc = _baseFs.CreateFile(in pathLockFile, 0);
                    if (rc.IsFailure()) return rc;

                    rc = _baseFs.OpenFile(ref _lockFile, in pathLockFile, OpenMode.ReadWrite);
                    if (rc.IsFailure()) return rc;
                }
                else
                {
                    return rc;
                }
            }

            return Result.Success;
        }

        private Result ResolvePath(ref Path outFullPath, in Path path)
        {
            using var pathDirectoryName = new Path();

            // Use the committed directory directly if journaling is supported but not enabled
            ReadOnlySpan<byte> directoryName = _isJournalingSupported && !_isJournalingEnabled
                ? CommittedDirectoryName
                : ModifiedDirectoryName;

            Result rc = PathFunctions.SetUpFixedPath(ref pathDirectoryName.Ref(), directoryName);
            if (rc.IsFailure()) return rc;

            rc = outFullPath.Combine(in pathDirectoryName, in path);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
        {
            using var fullPath = new Path();
            Result rc = ResolvePath(ref fullPath.Ref(), in path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            rc = _baseFs.CreateFile(in fullPath, size, option);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoDeleteFile(in Path path)
        {
            using var fullPath = new Path();
            Result rc = ResolvePath(ref fullPath.Ref(), in path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            rc = _baseFs.DeleteFile(in fullPath);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoCreateDirectory(in Path path)
        {
            using var fullPath = new Path();
            Result rc = ResolvePath(ref fullPath.Ref(), in path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            rc = _baseFs.CreateDirectory(in fullPath);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoDeleteDirectory(in Path path)
        {
            using var fullPath = new Path();
            Result rc = ResolvePath(ref fullPath.Ref(), in path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            rc = _baseFs.DeleteDirectory(in fullPath);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoDeleteDirectoryRecursively(in Path path)
        {
            using var fullPath = new Path();
            Result rc = ResolvePath(ref fullPath.Ref(), in path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            rc = _baseFs.DeleteDirectoryRecursively(in fullPath);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoCleanDirectoryRecursively(in Path path)
        {
            using var fullPath = new Path();
            Result rc = ResolvePath(ref fullPath.Ref(), in path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            rc = _baseFs.CleanDirectoryRecursively(in fullPath);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoRenameFile(in Path currentPath, in Path newPath)
        {
            using var currentFullPath = new Path();
            using var newFullPath = new Path();

            Result rc = ResolvePath(ref currentFullPath.Ref(), in currentPath);
            if (rc.IsFailure()) return rc;

            rc = ResolvePath(ref newFullPath.Ref(), in newPath);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            rc = _baseFs.RenameFile(in currentFullPath, in newFullPath);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
        {
            using var currentFullPath = new Path();
            using var newFullPath = new Path();

            Result rc = ResolvePath(ref currentFullPath.Ref(), in currentPath);
            if (rc.IsFailure()) return rc;

            rc = ResolvePath(ref newFullPath.Ref(), in newPath);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            rc = _baseFs.RenameDirectory(in currentFullPath, in newFullPath);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            using var fullPath = new Path();
            Result rc = ResolvePath(ref fullPath.Ref(), in path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            rc = _baseFs.GetEntryType(out entryType, in fullPath);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
        {
            using var fullPath = new Path();
            Result rc = ResolvePath(ref fullPath.Ref(), in path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            using var baseFile = new UniqueRef<IFile>();
            rc = _baseFs.OpenFile(ref baseFile.Ref(), in fullPath, mode);
            if (rc.IsFailure()) return rc;

            using var file = new UniqueRef<IFile>(new DirectorySaveDataFile(ref baseFile.Ref(), this, mode));

            if (mode.HasFlag(OpenMode.Write))
            {
                _openWritableFileCount++;
            }

            outFile.Set(ref file.Ref());
            return Result.Success;
        }

        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
            OpenDirectoryMode mode)
        {
            using var fullPath = new Path();
            Result rc = ResolvePath(ref fullPath.Ref(), in path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            rc = _baseFs.OpenDirectory(ref outDirectory, in fullPath, mode);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        /// <summary>
        /// Creates the destination directory if needed and copies the source directory to it.
        /// </summary>
        /// <param name="destPath">The path of the destination directory.</param>
        /// <param name="sourcePath">The path of the source directory.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private Result SynchronizeDirectory(in Path destPath, in Path sourcePath)
        {
            // Delete destination dir and recreate it.
            Result rc = _baseFs.DeleteDirectoryRecursively(destPath);

            // Nintendo returns all errors unconditionally because SynchronizeDirectory is always called in situations
            // where a PathNotFound error would mean the save directory was in an invalid state.
            // We'll ignore PathNotFound errors to be more user-friendly to users who might accidentally
            // put the save directory in an invalid state.
            if (rc.IsFailure() && !ResultFs.PathNotFound.Includes(rc)) return rc;

            rc = _baseFs.CreateDirectory(destPath);
            if (rc.IsFailure()) return rc;

            var directoryEntry = new DirectoryEntry();

            // Lock only if initialized with a client
            if (_fsClient is not null)
            {
                using ScopedLock<SdkMutexType> scopedLock =
                    ScopedLock.Lock(ref _fsClient.Globals.DirectorySaveDataFileSystem.SynchronizeDirectoryMutex);

                using (var buffer = new RentedArray<byte>(IdealWorkBufferSize))
                {
                    return Utility.CopyDirectoryRecursively(_baseFs, in destPath, in sourcePath, ref directoryEntry,
                        buffer.Span);
                }
            }
            else
            {
                using (var buffer = new RentedArray<byte>(IdealWorkBufferSize))
                {
                    return Utility.CopyDirectoryRecursively(_baseFs, in destPath, in sourcePath, ref directoryEntry,
                        buffer.Span);
                }
            }
        }

        protected override Result DoCommit()
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            if (!_isJournalingEnabled || !_isJournalingSupported)
                return Result.Success;

            var closure = new RetryClosure();
            closure.This = this;

            Result rc = PathFunctions.SetUpFixedPath(ref closure.ModifiedPath, ModifiedDirectoryName);
            if (rc.IsFailure()) return rc;

            rc = PathFunctions.SetUpFixedPath(ref closure.CommittedPath, CommittedDirectoryName);
            if (rc.IsFailure()) return rc;

            rc = PathFunctions.SetUpFixedPath(ref closure.SynchronizingPath, SynchronizingDirectoryName);
            if (rc.IsFailure()) return rc;

            if (_openWritableFileCount > 0)
            {
                // All files must be closed before commiting save data.
                return ResultFs.WriteModeFileNotClosed.Log();
            }

            static Result RenameCommittedDir(in RetryClosure closure)
            {
                return closure.This._baseFs.RenameDirectory(in closure.CommittedPath,
                    in closure.SynchronizingPath);
            }

            static Result SynchronizeWorkingDir(in RetryClosure closure)
            {
                return closure.This.SynchronizeDirectory(in closure.SynchronizingPath,
                    in closure.ModifiedPath);
            }

            static Result RenameSynchronizingDir(in RetryClosure closure)
            {
                return closure.This._baseFs.RenameDirectory(in closure.SynchronizingPath,
                    in closure.CommittedPath);
            }

            // Get rid of the previous commit by renaming the folder.
            rc = RetryFinitelyForTargetLocked(RenameCommittedDir, in closure);
            if (rc.IsFailure()) return rc;

            // If something goes wrong beyond this point, the commit will be
            // completed the next time the savedata is opened.

            rc = RetryFinitelyForTargetLocked(SynchronizeWorkingDir, in closure);
            if (rc.IsFailure()) return rc;

            rc = RetryFinitelyForTargetLocked(RenameSynchronizingDir, in closure);
            if (rc.IsFailure()) return rc;

            closure.Dispose();
            return Result.Success;
        }

        protected override Result DoCommitProvisionally(long counter)
        {
            if (!_isMultiCommitSupported)
                return ResultFs.UnsupportedCommitProvisionallyForDirectorySaveDataFileSystem.Log();

            return Result.Success;
        }

        protected override Result DoRollback()
        {
            // No old data is kept for non-journaling save data, so there's nothing to rollback to
            if (!_isJournalingSupported)
                return Result.Success;

            return Initialize(_isJournalingSupported, _isMultiCommitSupported, _isJournalingEnabled);
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out freeSpace);

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            using var pathModifiedDirectory = new Path();
            Result rc = PathFunctions.SetUpFixedPath(ref pathModifiedDirectory.Ref(), ModifiedDirectoryName);
            if (rc.IsFailure()) return rc;

            rc = _baseFs.GetFreeSpaceSize(out freeSpace, in pathModifiedDirectory);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out totalSpace);

            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            using var pathModifiedDirectory = new Path();
            Result rc = PathFunctions.SetUpFixedPath(ref pathModifiedDirectory.Ref(), ModifiedDirectoryName);
            if (rc.IsFailure()) return rc;

            rc = _baseFs.GetTotalSpaceSize(out totalSpace, in pathModifiedDirectory);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        private void DecrementWriteOpenFileCount()
        {
            // Todo?: Calling OpenFile when outFile already contains a DirectorySaveDataFile
            // will try to lock this mutex a second time
            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            _openWritableFileCount--;
        }

        // The original class doesn't support extra data.
        // Everything below this point is a LibHac extension.

        private static ReadOnlySpan<byte> CommittedExtraDataName => // "/ExtraData0"
            new[]
            {
                (byte)'/', (byte)'E', (byte)'x', (byte)'t', (byte)'r', (byte)'a', (byte)'D', (byte)'a',
                (byte)'t', (byte)'a', (byte)'0'
            };

        private static ReadOnlySpan<byte> ModifiedExtraDataName => // "/ExtraData1"
            new[]
            {
                (byte)'/', (byte)'E', (byte)'x', (byte)'t', (byte)'r', (byte)'a', (byte)'D', (byte)'a',
                (byte)'t', (byte)'a', (byte)'1'
            };

        private static ReadOnlySpan<byte> SynchronizingExtraDataName => // "/ExtraData_"
            new[]
            {
                (byte)'/', (byte)'E', (byte)'x', (byte)'t', (byte)'r', (byte)'a', (byte)'D', (byte)'a',
                (byte)'t', (byte)'a', (byte)'_'
            };

        private Result InitializeExtraData()
        {
            using var pathModifiedExtraData = new Path();
            Result rc = PathFunctions.SetUpFixedPath(ref pathModifiedExtraData.Ref(), ModifiedExtraDataName);
            if (rc.IsFailure()) return rc;

            using var pathCommittedExtraData = new Path();
            rc = PathFunctions.SetUpFixedPath(ref pathCommittedExtraData.Ref(), CommittedExtraDataName);
            if (rc.IsFailure()) return rc;

            using var pathSynchronizingExtraData = new Path();
            rc = PathFunctions.SetUpFixedPath(ref pathSynchronizingExtraData.Ref(), SynchronizingExtraDataName);
            if (rc.IsFailure()) return rc;

            // Ensure the extra data files exist
            rc = _baseFs.GetEntryType(out _, in pathModifiedExtraData);

            if (rc.IsFailure())
            {
                if (!ResultFs.PathNotFound.Includes(rc))
                    return rc;

                rc = _baseFs.CreateFile(in pathModifiedExtraData, Unsafe.SizeOf<SaveDataExtraData>());
                if (rc.IsFailure()) return rc;

                if (_isJournalingSupported)
                {
                    rc = _baseFs.CreateFile(in pathCommittedExtraData, Unsafe.SizeOf<SaveDataExtraData>());
                    if (rc.IsFailure() && !ResultFs.PathAlreadyExists.Includes(rc))
                        return rc;
                }
            }
            else
            {
                // If the working file exists make sure it's the right size
                rc = EnsureExtraDataSize(in pathModifiedExtraData);
                if (rc.IsFailure()) return rc;
            }

            // Only the working extra data is needed for non-journaling savedata
            if (_isJournalingSupported)
            {
                rc = _baseFs.GetEntryType(out _, in pathCommittedExtraData);

                if (rc.IsSuccess())
                {
                    rc = EnsureExtraDataSize(in pathCommittedExtraData);
                    if (rc.IsFailure()) return rc;

                    if (_isJournalingEnabled)
                    {
                        rc = SynchronizeExtraData(in pathModifiedExtraData, in pathCommittedExtraData);
                        if (rc.IsFailure()) return rc;
                    }
                }
                else if (ResultFs.PathNotFound.Includes(rc))
                {
                    // If a previous commit failed, the committed extra data may be missing.
                    // Finish that commit by copying the working extra data to the committed extra data
                    rc = SynchronizeExtraData(in pathSynchronizingExtraData, in pathModifiedExtraData);
                    if (rc.IsFailure()) return rc;

                    rc = _baseFs.RenameFile(in pathSynchronizingExtraData, in pathCommittedExtraData);
                    if (rc.IsFailure()) return rc;
                }
                else
                {
                    return rc;
                }
            }

            return Result.Success;
        }

        private Result EnsureExtraDataSize(in Path path)
        {
            using var file = new UniqueRef<IFile>();
            Result rc = _baseFs.OpenFile(ref file.Ref(), in path, OpenMode.ReadWrite);
            if (rc.IsFailure()) return rc;

            rc = file.Get.GetSize(out long fileSize);
            if (rc.IsFailure()) return rc;

            if (fileSize == Unsafe.SizeOf<SaveDataExtraData>())
                return Result.Success;

            return file.Get.SetSize(Unsafe.SizeOf<SaveDataExtraData>());
        }

        private Result SynchronizeExtraData(in Path destPath, in Path sourcePath)
        {
            Span<byte> workBuffer = stackalloc byte[Unsafe.SizeOf<SaveDataExtraData>()];

            using (var sourceFile = new UniqueRef<IFile>())
            {
                Result rc = _baseFs.OpenFile(ref sourceFile.Ref(), in sourcePath, OpenMode.Read);
                if (rc.IsFailure()) return rc;

                rc = sourceFile.Get.Read(out long bytesRead, 0, workBuffer);
                if (rc.IsFailure()) return rc;

                Assert.SdkEqual(bytesRead, Unsafe.SizeOf<SaveDataExtraData>());
            }

            using (var destFile = new UniqueRef<IFile>())
            {
                Result rc = _baseFs.OpenFile(ref destFile.Ref(), in destPath, OpenMode.Write);
                if (rc.IsFailure()) return rc;

                rc = destFile.Get.Write(0, workBuffer, WriteOption.Flush);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        private Result GetExtraDataPath(ref Path path)
        {
            ReadOnlySpan<byte> extraDataName = _isJournalingSupported && !_isJournalingEnabled
                ? CommittedExtraDataName
                : ModifiedExtraDataName;

            return PathFunctions.SetUpFixedPath(ref path, extraDataName);
        }

        public Result WriteExtraData(in SaveDataExtraData extraData)
        {
            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            return WriteExtraDataImpl(in extraData);
        }

        public Result CommitExtraData(bool updateTimeStamp)
        {
            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            if (updateTimeStamp && _timeStampGetter is not null && _randomGenerator is not null)
            {
                Result rc = UpdateExtraDataTimeStamp();
                if (rc.IsFailure()) return rc;
            }

            return CommitExtraDataImpl();
        }

        public Result ReadExtraData(out SaveDataExtraData extraData)
        {
            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

            return ReadExtraDataImpl(out extraData);
        }

        private Result UpdateExtraDataTimeStamp()
        {
            Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

            Result rc = ReadExtraDataImpl(out SaveDataExtraData extraData);
            if (rc.IsFailure()) return rc;

            if (_timeStampGetter.Get(out long timeStamp).IsSuccess())
            {
                extraData.TimeStamp = timeStamp;
            }

            long commitId = 0;

            do
            {
                _randomGenerator(SpanHelpers.AsByteSpan(ref commitId));
            } while (commitId == 0 || commitId == extraData.CommitId);

            extraData.CommitId = commitId;

            return WriteExtraDataImpl(in extraData);
        }

        private Result WriteExtraDataImpl(in SaveDataExtraData extraData)
        {
            Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

            using var pathExtraData = new Path();
            Result rc = GetExtraDataPath(ref pathExtraData.Ref());
            if (rc.IsFailure()) return rc;

            using var file = new UniqueRef<IFile>();
            rc = _baseFs.OpenFile(ref file.Ref(), in pathExtraData, OpenMode.Write);
            if (rc.IsFailure()) return rc;

            rc = file.Get.Write(0, SpanHelpers.AsReadOnlyByteSpan(in extraData), WriteOption.Flush);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        private Result CommitExtraDataImpl()
        {
            Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

            if (!_isJournalingSupported || !_isJournalingEnabled)
                return Result.Success;

            var closure = new RetryClosure();
            closure.This = this;

            Result rc = PathFunctions.SetUpFixedPath(ref closure.ModifiedPath, ModifiedExtraDataName);
            if (rc.IsFailure()) return rc;

            rc = PathFunctions.SetUpFixedPath(ref closure.CommittedPath, CommittedExtraDataName);
            if (rc.IsFailure()) return rc;

            rc = PathFunctions.SetUpFixedPath(ref closure.SynchronizingPath, SynchronizingExtraDataName);
            if (rc.IsFailure()) return rc;

            static Result RenameCommittedFile(in RetryClosure closure)
            {
                return closure.This._baseFs.RenameFile(in closure.CommittedPath,
                    in closure.SynchronizingPath);
            }

            static Result SynchronizeWorkingFile(in RetryClosure closure)
            {
                return closure.This.SynchronizeExtraData(in closure.SynchronizingPath,
                    in closure.ModifiedPath);
            }

            static Result RenameSynchronizingFile(in RetryClosure closure)
            {
                return closure.This._baseFs.RenameFile(in closure.SynchronizingPath,
                    in closure.CommittedPath);
            }

            // Get rid of the previous commit by renaming the file.
            rc = RetryFinitelyForTargetLocked(RenameCommittedFile, in closure);
            if (rc.IsFailure()) return rc;

            // If something goes wrong beyond this point, the commit will be
            // completed the next time the savedata is opened.

            rc = RetryFinitelyForTargetLocked(SynchronizeWorkingFile, in closure);
            if (rc.IsFailure()) return rc;

            rc = RetryFinitelyForTargetLocked(RenameSynchronizingFile, in closure);
            if (rc.IsFailure()) return rc;

            closure.Dispose();
            return Result.Success;
        }

        private Result ReadExtraDataImpl(out SaveDataExtraData extraData)
        {
            UnsafeHelpers.SkipParamInit(out extraData);

            Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

            using var pathExtraData = new Path();
            Result rc = GetExtraDataPath(ref pathExtraData.Ref());
            if (rc.IsFailure()) return rc;

            using var file = new UniqueRef<IFile>();
            rc = _baseFs.OpenFile(ref file.Ref(), in pathExtraData, OpenMode.Read);
            if (rc.IsFailure()) return rc;

            rc = file.Get.Read(out long bytesRead, 0, SpanHelpers.AsByteSpan(ref extraData));
            if (rc.IsFailure()) return rc;

            Assert.SdkEqual(bytesRead, Unsafe.SizeOf<SaveDataExtraData>());

            return Result.Success;
        }

        public void RegisterCacheObserver(ISaveDataExtraDataAccessorCacheObserver observer, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            _cacheObserver = observer;
            _spaceId = spaceId;
            _saveDataId = saveDataId;
        }

        public SaveDataSpaceId GetSaveDataSpaceId() => _spaceId;
        public ulong GetSaveDataId() => _saveDataId;
    }
}
