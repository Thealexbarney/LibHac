using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv;
using LibHac.Os;
using LibHac.Util;

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
    /// <br/>Based on FS 11.0.0 (nnSdk 11.4.0)
    /// </remarks>
    public class DirectorySaveDataFileSystem : IFileSystem, ISaveDataExtraDataAccessor
    {
        private const int IdealWorkBufferSize = 0x100000; // 1 MiB

        private static ReadOnlySpan<byte> CommittedDirectoryBytes => new[] { (byte)'/', (byte)'0', (byte)'/' };
        private static ReadOnlySpan<byte> WorkingDirectoryBytes => new[] { (byte)'/', (byte)'1', (byte)'/' };
        private static ReadOnlySpan<byte> SynchronizingDirectoryBytes => new[] { (byte)'/', (byte)'_', (byte)'/' };
        private static ReadOnlySpan<byte> LockFileBytes => new[] { (byte)'/', (byte)'.', (byte)'l', (byte)'o', (byte)'c', (byte)'k' };

        private static U8Span CommittedDirectoryPath => new U8Span(CommittedDirectoryBytes);
        private static U8Span WorkingDirectoryPath => new U8Span(WorkingDirectoryBytes);
        private static U8Span SynchronizingDirectoryPath => new U8Span(SynchronizingDirectoryBytes);
        private static U8Span LockFilePath => new U8Span(LockFileBytes);

        private FileSystemClient _fsClient;
        private IFileSystem _baseFs;

        private SdkMutexType _mutex;

        // Todo: Unique file system for disposal
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
        private IFile _lockFile;

        private class DirectorySaveDataFile : IFile
        {
            private IFile _baseFile;
            private DirectorySaveDataFileSystem _parentFs;
            private OpenMode _mode;

            public DirectorySaveDataFile(DirectorySaveDataFileSystem parentFs, IFile baseFile, OpenMode mode)
            {
                _parentFs = parentFs;
                _baseFile = baseFile;
                _mode = mode;
            }

            protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
                in ReadOption option)
            {
                return _baseFile.Read(out bytesRead, offset, destination, in option);
            }

            protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
            {
                return _baseFile.Write(offset, source, in option);
            }

            protected override Result DoFlush()
            {
                return _baseFile.Flush();
            }

            protected override Result DoGetSize(out long size)
            {
                return _baseFile.GetSize(out size);
            }

            protected override Result DoSetSize(long size)
            {
                return _baseFile.SetSize(size);
            }

            protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset,
                long size, ReadOnlySpan<byte> inBuffer)
            {
                return _baseFile.OperateRange(outBuffer, operationId, offset, size, inBuffer);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _baseFile?.Dispose();

                    if (_mode.HasFlag(OpenMode.Write))
                    {
                        _parentFs.DecrementWriteOpenFileCount();
                        _mode = default;
                    }
                }

                base.Dispose(disposing);
            }
        }

        public static Result CreateNew(out DirectorySaveDataFileSystem created, IFileSystem baseFileSystem,
            ISaveDataCommitTimeStampGetter timeStampGetter, RandomDataGenerator randomGenerator,
            bool isJournalingSupported, bool isMultiCommitSupported, bool isJournalingEnabled,
            FileSystemClient fsClient)
        {
            var obj = new DirectorySaveDataFileSystem(baseFileSystem, fsClient);
            Result rc = obj.Initialize(timeStampGetter, randomGenerator, isJournalingSupported, isMultiCommitSupported,
                isJournalingEnabled);

            if (rc.IsSuccess())
            {
                created = obj;
                return Result.Success;
            }

            obj.Dispose();
            UnsafeHelpers.SkipParamInit(out created);
            return rc;
        }

        public static Result CreateNew(out DirectorySaveDataFileSystem created, IFileSystem baseFileSystem,
            bool isJournalingSupported, bool isMultiCommitSupported, bool isJournalingEnabled)
        {
            return CreateNew(out created, baseFileSystem, null, null, isJournalingSupported, isMultiCommitSupported,
                isJournalingEnabled, null);
        }

        public static ReferenceCountedDisposable<DirectorySaveDataFileSystem> CreateShared(IFileSystem baseFileSystem,
            FileSystemClient fsClient)
        {
            var fileSystem = new DirectorySaveDataFileSystem(baseFileSystem, fsClient);
            return new ReferenceCountedDisposable<DirectorySaveDataFileSystem>(fileSystem);
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

        protected override void Dispose(bool disposing)
        {
            _lockFile?.Dispose();
            _lockFile = null;

            _cacheObserver?.Unregister(_spaceId, _saveDataId);
            _baseFs?.Dispose();
            base.Dispose(disposing);
        }

        public Result Initialize(bool isJournalingSupported, bool isMultiCommitSupported, bool isJournalingEnabled)
        {
            return Initialize(null, null, isJournalingSupported, isMultiCommitSupported, isJournalingEnabled);
        }

        public Result Initialize(ISaveDataCommitTimeStampGetter timeStampGetter, RandomDataGenerator randomGenerator,
            bool isJournalingSupported, bool isMultiCommitSupported, bool isJournalingEnabled)
        {
            Result rc;

            _isJournalingSupported = isJournalingSupported;
            _isMultiCommitSupported = isMultiCommitSupported;
            _isJournalingEnabled = isJournalingEnabled;
            _timeStampGetter = timeStampGetter ?? _timeStampGetter;
            _randomGenerator = randomGenerator ?? _randomGenerator;

            // Open the lock file
            if (_lockFile is null)
            {
                rc = _baseFs.OpenFile(out _lockFile, LockFilePath, OpenMode.ReadWrite);

                if (rc.IsFailure())
                {
                    if (!ResultFs.PathNotFound.Includes(rc)) return rc;

                    rc = _baseFs.CreateFile(LockFilePath, 0);
                    if (rc.IsFailure()) return rc;

                    rc = _baseFs.OpenFile(out _lockFile, LockFilePath, OpenMode.ReadWrite);
                    if (rc.IsFailure()) return rc;
                }
            }

            // Ensure the working directory exists
            rc = _baseFs.GetEntryType(out _, WorkingDirectoryPath);

            if (rc.IsFailure())
            {
                if (!ResultFs.PathNotFound.Includes(rc)) return rc;

                rc = _baseFs.CreateDirectory(WorkingDirectoryPath);
                if (rc.IsFailure()) return rc;

                if (_isJournalingSupported)
                {
                    rc = _baseFs.CreateDirectory(CommittedDirectoryPath);

                    // Nintendo returns on all failures, but we'll keep going if committed already exists
                    // to avoid confusing people manually creating savedata in emulators
                    if (rc.IsFailure() && !ResultFs.PathAlreadyExists.Includes(rc)) return rc;
                }
            }

            // Only the working directory is needed for non-journaling savedata
            if (!_isJournalingSupported)
                return InitializeExtraData();

            rc = _baseFs.GetEntryType(out _, CommittedDirectoryPath);

            if (rc.IsSuccess())
            {
                if (!_isJournalingEnabled)
                    return InitializeExtraData();

                rc = SynchronizeDirectory(WorkingDirectoryPath, CommittedDirectoryPath);
                if (rc.IsFailure()) return rc;

                return InitializeExtraData();
            }

            if (!ResultFs.PathNotFound.Includes(rc)) return rc;

            // If a previous commit failed, the committed dir may be missing.
            // Finish that commit by copying the working dir to the committed dir

            rc = SynchronizeDirectory(SynchronizingDirectoryPath, WorkingDirectoryPath);
            if (rc.IsFailure()) return rc;

            rc = _baseFs.RenameDirectory(SynchronizingDirectoryPath, CommittedDirectoryPath);
            if (rc.IsFailure()) return rc;

            rc = InitializeExtraData();
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        private Result ResolveFullPath(Span<byte> outPath, U8Span relativePath)
        {
            if (StringUtils.GetLength(relativePath, PathTools.MaxPathLength + 1) > PathTools.MaxPathLength)
                return ResultFs.TooLongPath.Log();

            // Use the committed directory directly if journaling is supported but not enabled
            U8Span workingPath = _isJournalingSupported && !_isJournalingEnabled
                ? CommittedDirectoryPath
                : WorkingDirectoryPath;

            StringUtils.Copy(outPath, workingPath);
            outPath[outPath.Length - 1] = StringTraits.NullTerminator;

            return PathNormalizer.Normalize(outPath.Slice(2), out _, relativePath, false, false);
        }

        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions options)
        {
            Unsafe.SkipInit(out FsPath fullPath);

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            return _baseFs.CreateFile(fullPath, size, options);
        }

        protected override Result DoDeleteFile(U8Span path)
        {
            Unsafe.SkipInit(out FsPath fullPath);

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            return _baseFs.DeleteFile(fullPath);
        }

        protected override Result DoCreateDirectory(U8Span path)
        {
            Unsafe.SkipInit(out FsPath fullPath);

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            return _baseFs.CreateDirectory(fullPath);
        }

        protected override Result DoDeleteDirectory(U8Span path)
        {
            Unsafe.SkipInit(out FsPath fullPath);

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            return _baseFs.DeleteDirectory(fullPath);
        }

        protected override Result DoDeleteDirectoryRecursively(U8Span path)
        {
            Unsafe.SkipInit(out FsPath fullPath);

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            return _baseFs.DeleteDirectoryRecursively(fullPath);
        }

        protected override Result DoCleanDirectoryRecursively(U8Span path)
        {
            Unsafe.SkipInit(out FsPath fullPath);

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            return _baseFs.CleanDirectoryRecursively(fullPath);
        }

        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath)
        {
            Unsafe.SkipInit(out FsPath fullCurrentPath);
            Unsafe.SkipInit(out FsPath fullNewPath);

            Result rc = ResolveFullPath(fullCurrentPath.Str, oldPath);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(fullNewPath.Str, newPath);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            return _baseFs.RenameFile(fullCurrentPath, fullNewPath);
        }

        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath)
        {
            Unsafe.SkipInit(out FsPath fullCurrentPath);
            Unsafe.SkipInit(out FsPath fullNewPath);

            Result rc = ResolveFullPath(fullCurrentPath.Str, oldPath);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(fullNewPath.Str, newPath);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            return _baseFs.RenameDirectory(fullCurrentPath, fullNewPath);
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            Unsafe.SkipInit(out FsPath fullPath);

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure())
            {
                UnsafeHelpers.SkipParamInit(out entryType);
                return rc;
            }

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            return _baseFs.GetEntryType(out entryType, fullPath);
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            Unsafe.SkipInit(out FsPath fullPath);

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            rc = _baseFs.OpenFile(out IFile baseFile, fullPath, mode);
            if (rc.IsFailure()) return rc;

            file = new DirectorySaveDataFile(this, baseFile, mode);

            if (mode.HasFlag(OpenMode.Write))
            {
                _openWritableFileCount++;
            }

            return Result.Success;
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);

            Unsafe.SkipInit(out FsPath fullPath);

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            return _baseFs.OpenDirectory(out directory, fullPath, mode);
        }

        /// <summary>
        /// Creates the destination directory if needed and copies the source directory to it.
        /// </summary>
        /// <param name="destPath">The path of the destination directory.</param>
        /// <param name="sourcePath">The path of the source directory.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private Result SynchronizeDirectory(U8Span destPath, U8Span sourcePath)
        {
            // Delete destination dir and recreate it.
            Result rc = _baseFs.DeleteDirectoryRecursively(destPath);

            // Nintendo returns error unconditionally because SynchronizeDirectory is always called in situations
            // where a PathNotFound error would mean the save directory was in an invalid state.
            // We'll ignore PathNotFound errors to be more user-friendly to users who might accidentally
            // put the save directory in an invalid state.
            if (rc.IsFailure() && !ResultFs.PathNotFound.Includes(rc)) return rc;

            rc = _baseFs.CreateDirectory(destPath);
            if (rc.IsFailure()) return rc;

            // Lock only if initialized with a client
            if (_fsClient is not null)
            {
                using ScopedLock<SdkMutexType> lk =
                    ScopedLock.Lock(ref _fsClient.Globals.DirectorySaveDataFileSystem.SynchronizeDirectoryMutex);

                using (var buffer = new RentedArray<byte>(IdealWorkBufferSize))
                {
                    return Utility.CopyDirectoryRecursively(_baseFs, destPath, sourcePath, buffer.Span);
                }
            }
            else
            {
                using (var buffer = new RentedArray<byte>(IdealWorkBufferSize))
                {
                    return Utility.CopyDirectoryRecursively(_baseFs, destPath, sourcePath, buffer.Span);
                }
            }
        }

        protected override Result DoCommit()
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            if (!_isJournalingEnabled || !_isJournalingSupported)
                return Result.Success;

            if (_openWritableFileCount > 0)
            {
                // All files must be closed before commiting save data.
                return ResultFs.WriteModeFileNotClosed.Log();
            }

            Result RenameCommittedDir() => _baseFs.RenameDirectory(CommittedDirectoryPath, SynchronizingDirectoryPath);
            Result SynchronizeWorkingDir() => SynchronizeDirectory(SynchronizingDirectoryPath, WorkingDirectoryPath);

            Result RenameSynchronizingDir() =>
                _baseFs.RenameDirectory(SynchronizingDirectoryPath, CommittedDirectoryPath);

            // Get rid of the previous commit by renaming the folder
            Result rc = Utility.RetryFinitelyForTargetLocked(RenameCommittedDir);
            if (rc.IsFailure()) return rc;

            // If something goes wrong beyond this point, the commit will be
            // completed the next time the savedata is opened

            rc = Utility.RetryFinitelyForTargetLocked(SynchronizeWorkingDir);
            if (rc.IsFailure()) return rc;

            rc = Utility.RetryFinitelyForTargetLocked(RenameSynchronizingDir);
            if (rc.IsFailure()) return rc;

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

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out freeSpace);

            Unsafe.SkipInit(out FsPath fullPath);

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            return _baseFs.GetFreeSpaceSize(out freeSpace, fullPath);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out totalSpace);

            Unsafe.SkipInit(out FsPath fullPath);

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            return _baseFs.GetTotalSpaceSize(out totalSpace, fullPath);
        }

        internal void DecrementWriteOpenFileCount()
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            _openWritableFileCount--;
        }

        // The original class doesn't support extra data.
        // Everything below this point is a LibHac extension.

        private static ReadOnlySpan<byte> CommittedExtraDataBytes => // "/ExtraData0"
            new[]
            {
                (byte)'/', (byte)'E', (byte)'x', (byte)'t', (byte)'r', (byte)'a', (byte)'D', (byte)'a',
                (byte)'t', (byte)'a', (byte)'0'
            };

        private static ReadOnlySpan<byte> WorkingExtraDataBytes => // "/ExtraData1"
            new[]
            {
                (byte)'/', (byte)'E', (byte)'x', (byte)'t', (byte)'r', (byte)'a', (byte)'D', (byte)'a',
                (byte)'t', (byte)'a', (byte)'1'
            };

        private static ReadOnlySpan<byte> SynchronizingExtraDataBytes => // "/ExtraData_"
            new[]
            {
                (byte)'/', (byte)'E', (byte)'x', (byte)'t', (byte)'r', (byte)'a', (byte)'D', (byte)'a',
                (byte)'t', (byte)'a', (byte)'_'
            };

        private U8Span CommittedExtraDataPath => new U8Span(CommittedExtraDataBytes);
        private U8Span WorkingExtraDataPath => new U8Span(WorkingExtraDataBytes);
        private U8Span SynchronizingExtraDataPath => new U8Span(SynchronizingExtraDataBytes);

        private Result InitializeExtraData()
        {
            // Ensure the extra data files exist
            Result rc = _baseFs.GetEntryType(out _, WorkingExtraDataPath);

            if (rc.IsFailure())
            {
                if (!ResultFs.PathNotFound.Includes(rc)) return rc;

                rc = _baseFs.CreateFile(WorkingExtraDataPath, Unsafe.SizeOf<SaveDataExtraData>());
                if (rc.IsFailure()) return rc;

                if (_isJournalingSupported)
                {
                    rc = _baseFs.CreateFile(CommittedExtraDataPath, Unsafe.SizeOf<SaveDataExtraData>());
                    if (rc.IsFailure() && !ResultFs.PathAlreadyExists.Includes(rc)) return rc;
                }
            }
            else
            {
                // If the working file exists make sure it's the right size
                rc = EnsureExtraDataSize(WorkingExtraDataPath);
                if (rc.IsFailure()) return rc;
            }

            // Only the working extra data is needed for non-journaling savedata
            if (!_isJournalingSupported)
                return Result.Success;

            rc = _baseFs.GetEntryType(out _, CommittedExtraDataPath);

            if (rc.IsSuccess())
            {
                rc = EnsureExtraDataSize(CommittedExtraDataPath);
                if (rc.IsFailure()) return rc;

                if (!_isJournalingEnabled)
                    return Result.Success;

                return SynchronizeExtraData(WorkingExtraDataPath, CommittedExtraDataPath);
            }

            if (!ResultFs.PathNotFound.Includes(rc)) return rc;

            // If a previous commit failed, the committed extra data may be missing.
            // Finish that commit by copying the working extra data to the committed extra data

            rc = SynchronizeExtraData(SynchronizingExtraDataPath, WorkingExtraDataPath);
            if (rc.IsFailure()) return rc;

            rc = _baseFs.RenameFile(SynchronizingExtraDataPath, CommittedExtraDataPath);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        private Result EnsureExtraDataSize(U8Span path)
        {
            IFile file = null;
            try
            {
                Result rc = _baseFs.OpenFile(out file, path, OpenMode.ReadWrite);
                if (rc.IsFailure()) return rc;

                rc = file.GetSize(out long fileSize);
                if (rc.IsFailure()) return rc;

                if (fileSize == Unsafe.SizeOf<SaveDataExtraData>())
                    return Result.Success;

                return file.SetSize(Unsafe.SizeOf<SaveDataExtraData>());
            }
            finally
            {
                file?.Dispose();
            }
        }

        private Result SynchronizeExtraData(U8Span destPath, U8Span sourcePath)
        {
            Span<byte> workBuffer = stackalloc byte[Unsafe.SizeOf<SaveDataExtraData>()];

            Result rc = _baseFs.OpenFile(out IFile sourceFile, sourcePath, OpenMode.Read);
            if (rc.IsFailure()) return rc;

            using (sourceFile)
            {
                rc = sourceFile.Read(out long bytesRead, 0, workBuffer);
                if (rc.IsFailure()) return rc;

                Assert.SdkEqual(bytesRead, Unsafe.SizeOf<SaveDataExtraData>());
            }

            rc = _baseFs.OpenFile(out IFile destFile, destPath, OpenMode.Write);
            if (rc.IsFailure()) return rc;

            using (destFile)
            {
                rc = destFile.Write(0, workBuffer, WriteOption.Flush);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        private U8Span GetExtraDataPath()
        {
            return _isJournalingSupported && !_isJournalingEnabled
                ? CommittedExtraDataPath
                : WorkingExtraDataPath;
        }

        public Result WriteExtraData(in SaveDataExtraData extraData)
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            return WriteExtraDataImpl(in extraData);
        }

        public Result CommitExtraData(bool updateTimeStamp)
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            if (updateTimeStamp && _timeStampGetter is not null && _randomGenerator is not null)
            {
                Result rc = UpdateExtraDataTimeStamp();
                if (rc.IsFailure()) return rc;
            }

            return CommitExtraDataImpl();
        }

        public Result ReadExtraData(out SaveDataExtraData extraData)
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

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

            Result rc = _baseFs.OpenFile(out IFile file, GetExtraDataPath(), OpenMode.Write);
            if (rc.IsFailure()) return rc;

            using (file)
            {
                return file.Write(0, SpanHelpers.AsReadOnlyByteSpan(in extraData), WriteOption.Flush);
            }
        }

        private Result CommitExtraDataImpl()
        {
            Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

            if (!_isJournalingSupported || !_isJournalingEnabled)
                return Result.Success;

            Result RenameCommittedFile() => _baseFs.RenameFile(CommittedExtraDataPath, SynchronizingExtraDataPath);
            Result SynchronizeWorkingFile() => SynchronizeExtraData(SynchronizingExtraDataPath, WorkingExtraDataPath);
            Result RenameSynchronizingFile() => _baseFs.RenameFile(SynchronizingExtraDataPath, CommittedExtraDataPath);

            // Get rid of the previous commit by renaming the folder
            Result rc = Utility.RetryFinitelyForTargetLocked(RenameCommittedFile);
            if (rc.IsFailure()) return rc;

            // If something goes wrong beyond this point, the commit will be
            // completed the next time the savedata is opened

            rc = Utility.RetryFinitelyForTargetLocked(SynchronizeWorkingFile);
            if (rc.IsFailure()) return rc;

            rc = Utility.RetryFinitelyForTargetLocked(RenameSynchronizingFile);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        private Result ReadExtraDataImpl(out SaveDataExtraData extraData)
        {
            Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

            UnsafeHelpers.SkipParamInit(out extraData);

            Result rc = _baseFs.OpenFile(out IFile file, GetExtraDataPath(), OpenMode.Read);
            if (rc.IsFailure()) return rc;

            using (file)
            {
                rc = file.Read(out long bytesRead, 0, SpanHelpers.AsByteSpan(ref extraData));
                if (rc.IsFailure()) return rc;

                Assert.SdkEqual(bytesRead, Unsafe.SizeOf<SaveDataExtraData>());

                return Result.Success;
            }
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