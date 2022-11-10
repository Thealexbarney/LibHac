using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Os;

namespace LibHac.FsSystem;

internal struct DirectorySaveDataFileSystemGlobals
{
    public SdkMutexType SynchronizeDirectoryMutex;

    public void Initialize(FileSystemClient fsClient)
    {
        SynchronizeDirectoryMutex = new SdkMutexType();
    }
}

/// <summary>
/// An <see cref="IFileSystem"/> that provides transactional commits for savedata on top of another base IFileSystem.
/// </summary>
/// <remarks>
/// Transactional commits should be atomic as long as the <see cref="IFileSystem.RenameDirectory"/> function of the
/// underlying <see cref="IFileSystem"/> is atomic.
/// <para>Based on nnSdk 14.3.0 (FS 14.1.0)</para>
/// </remarks>
public class DirectorySaveDataFileSystem : ISaveDataFileSystem
{
    private const int IdealWorkBufferSize = 0x100000; // 1 MiB

    private static ReadOnlySpan<byte> CommittedDirectoryName => new[] { (byte)'/', (byte)'0' };
    private static ReadOnlySpan<byte> ModifiedDirectoryName => new[] { (byte)'/', (byte)'1' };
    private static ReadOnlySpan<byte> SynchronizingDirectoryName => new[] { (byte)'/', (byte)'_' };
    private static ReadOnlySpan<byte> LockFileName => new[] { (byte)'/', (byte)'.', (byte)'l', (byte)'o', (byte)'c', (byte)'k' };

    private IFileSystem _baseFs;
    private SdkMutexType _mutex;
    private UniqueRef<IFileSystem> _uniqueBaseFs;

    private int _openWritableFileCount;
    private bool _isJournalingSupported;
    private bool _isMultiCommitSupported;
    private bool _isJournalingEnabled;

    private ISaveDataExtraDataAccessorObserver _cacheObserver;
    private ulong _saveDataId;
    private SaveDataSpaceId _spaceId;

    private ISaveDataCommitTimeStampGetter _timeStampGetter;
    private RandomDataGenerator _randomGenerator;

    // LibHac additions
    private FileSystemClient _fsClient;

    // Addition to ensure only one directory save data fs is opened at a time
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
    /// If a <see cref="FileSystemClient"/> is provided a global mutex will be used when synchronizing directories.
    /// Running outside of a Horizon context doesn't require this mutex,
    /// and null can be passed to <paramref name="fsClient"/>.
    /// </summary>
    /// <param name="baseFileSystem">The base <see cref="IFileSystem"/> to use.</param>
    /// <param name="fsClient">The <see cref="FileSystemClient"/> to use. May be null.</param>
    public DirectorySaveDataFileSystem(IFileSystem baseFileSystem, FileSystemClient fsClient = null)
    {
        _baseFs = baseFileSystem;
        _mutex = new SdkMutexType();
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
    public DirectorySaveDataFileSystem(ref UniqueRef<IFileSystem> baseFileSystem, FileSystemClient fsClient = null)
    {
        _baseFs = baseFileSystem.Get;
        _mutex = new SdkMutexType();
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

        public RetryClosure(DirectorySaveDataFileSystem fs)
        {
            This = fs;
            CommittedPath = new Path();
            ModifiedPath = new Path();
            SynchronizingPath = new Path();
        }

        public void Dispose()
        {
            CommittedPath.Dispose();
            ModifiedPath.Dispose();
            SynchronizingPath.Dispose();
        }
    }

    private delegate Result RetryDelegate(in RetryClosure closure);

    private Result RetryFinitelyForTargetLocked(in RetryClosure closure, RetryDelegate function)
    {
        const int maxRetryCount = 10;
        const int retryWaitTimeMs = 100;

        int remainingRetries = maxRetryCount;

        while (true)
        {
            Result res = function(in closure);

            if (res.IsSuccess())
                return res;

            if (!ResultFs.TargetLocked.Includes(res))
                return res;

            if (remainingRetries <= 0)
                return res;

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

    public Result Initialize(bool isJournalingSupported, bool isMultiCommitSupported,
        bool isJournalingEnabled, ISaveDataCommitTimeStampGetter timeStampGetter, RandomDataGenerator randomGenerator)
    {
        _isJournalingSupported = isJournalingSupported;
        _isMultiCommitSupported = isMultiCommitSupported;
        _isJournalingEnabled = isJournalingEnabled;
        _timeStampGetter = timeStampGetter;
        _randomGenerator = randomGenerator;

        // Open the lock file
        Result res = AcquireLockFile();
        if (res.IsFailure()) return res.Miss();

        using var pathModifiedDirectory = new Path();
        res = PathFunctions.SetUpFixedPath(ref pathModifiedDirectory.Ref(), ModifiedDirectoryName);
        if (res.IsFailure()) return res.Miss();

        using var pathCommittedDirectory = new Path();
        res = PathFunctions.SetUpFixedPath(ref pathCommittedDirectory.Ref(), CommittedDirectoryName);
        if (res.IsFailure()) return res.Miss();

        using var pathSynchronizingDirectory = new Path();
        res = PathFunctions.SetUpFixedPath(ref pathSynchronizingDirectory.Ref(), SynchronizingDirectoryName);
        if (res.IsFailure()) return res.Miss();

        // Ensure the working directory exists
        res = _baseFs.GetEntryType(out _, in pathModifiedDirectory);

        if (res.IsFailure())
        {
            if (!ResultFs.PathNotFound.Includes(res))
                return res;

            res = _baseFs.CreateDirectory(in pathModifiedDirectory);
            if (res.IsFailure()) return res.Miss();

            if (_isJournalingSupported)
            {
                res = _baseFs.CreateDirectory(in pathCommittedDirectory);

                // Changed: Nintendo returns on all failures, but we'll keep going if committed already
                // exists to avoid confusing people manually creating savedata in emulators
                if (res.IsFailure() && !ResultFs.PathAlreadyExists.Includes(res))
                    return res;
            }
        }

        // Only the working directory is needed for non-journaling savedata
        if (_isJournalingSupported)
        {
            res = _baseFs.GetEntryType(out _, in pathCommittedDirectory);

            if (res.IsSuccess())
            {
                // The previous commit successfully completed. Copy the committed dir to the working dir.
                if (_isJournalingEnabled)
                {
                    res = SynchronizeDirectory(in pathModifiedDirectory, in pathCommittedDirectory);
                    if (res.IsFailure()) return res.Miss();
                }
            }
            else if (ResultFs.PathNotFound.Includes(res))
            {
                // If a previous commit failed, the committed dir may be missing.
                // Finish that commit by copying the working dir to the committed dir
                res = SynchronizeDirectory(in pathSynchronizingDirectory, in pathModifiedDirectory);
                if (res.IsFailure()) return res.Miss();

                res = _baseFs.RenameDirectory(in pathSynchronizingDirectory, in pathCommittedDirectory);
                if (res.IsFailure()) return res.Miss();
            }
            else
            {
                return res;
            }
        }

        res = InitializeExtraData();
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private Result AcquireLockFile()
    {
        // Having an open lock file means we already have the lock for the file system.
        if (_lockFile.HasValue)
            return Result.Success;

        using var pathLockFile = new Path();
        Result res = PathFunctions.SetUpFixedPath(ref pathLockFile.Ref(), LockFileName);
        if (res.IsFailure()) return res.Miss();

        using var lockFile = new UniqueRef<IFile>();
        res = _baseFs.OpenFile(ref lockFile.Ref(), in pathLockFile, OpenMode.ReadWrite);

        if (res.IsFailure())
        {
            if (ResultFs.PathNotFound.Includes(res))
            {
                res = _baseFs.CreateFile(in pathLockFile, 0);
                if (res.IsFailure()) return res.Miss();

                res = _baseFs.OpenFile(ref lockFile.Ref(), in pathLockFile, OpenMode.ReadWrite);
                if (res.IsFailure()) return res.Miss();
            }
            else
            {
                return res.Miss();
            }
        }

        _lockFile.Set(ref lockFile.Ref());
        return Result.Success;
    }

    private Result ResolvePath(ref Path outFullPath, in Path path)
    {
        using var pathDirectoryName = new Path();

        // Use the committed directory directly if journaling is supported but not enabled
        ReadOnlySpan<byte> directoryName = _isJournalingSupported && !_isJournalingEnabled
            ? CommittedDirectoryName
            : ModifiedDirectoryName;

        Result res = PathFunctions.SetUpFixedPath(ref pathDirectoryName.Ref(), directoryName);
        if (res.IsFailure()) return res.Miss();

        res = outFullPath.Combine(in pathDirectoryName, in path);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
    {
        using var fullPath = new Path();
        Result res = ResolvePath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        res = _baseFs.CreateFile(in fullPath, size, option);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoDeleteFile(in Path path)
    {
        using var fullPath = new Path();
        Result res = ResolvePath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        res = _baseFs.DeleteFile(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoCreateDirectory(in Path path)
    {
        using var fullPath = new Path();
        Result res = ResolvePath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        res = _baseFs.CreateDirectory(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoDeleteDirectory(in Path path)
    {
        using var fullPath = new Path();
        Result res = ResolvePath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        res = _baseFs.DeleteDirectory(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoDeleteDirectoryRecursively(in Path path)
    {
        using var fullPath = new Path();
        Result res = ResolvePath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        res = _baseFs.DeleteDirectoryRecursively(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoCleanDirectoryRecursively(in Path path)
    {
        using var fullPath = new Path();
        Result res = ResolvePath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        res = _baseFs.CleanDirectoryRecursively(in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoRenameFile(in Path currentPath, in Path newPath)
    {
        using var currentFullPath = new Path();
        using var newFullPath = new Path();

        Result res = ResolvePath(ref currentFullPath.Ref(), in currentPath);
        if (res.IsFailure()) return res.Miss();

        res = ResolvePath(ref newFullPath.Ref(), in newPath);
        if (res.IsFailure()) return res.Miss();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        res = _baseFs.RenameFile(in currentFullPath, in newFullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
    {
        using var currentFullPath = new Path();
        using var newFullPath = new Path();

        Result res = ResolvePath(ref currentFullPath.Ref(), in currentPath);
        if (res.IsFailure()) return res.Miss();

        res = ResolvePath(ref newFullPath.Ref(), in newPath);
        if (res.IsFailure()) return res.Miss();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        res = _baseFs.RenameDirectory(in currentFullPath, in newFullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out entryType);

        using var fullPath = new Path();
        Result res = ResolvePath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        res = _baseFs.GetEntryType(out entryType, in fullPath);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        using var fullPath = new Path();
        Result res = ResolvePath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        using var baseFile = new UniqueRef<IFile>();
        res = _baseFs.OpenFile(ref baseFile.Ref(), in fullPath, mode);
        if (res.IsFailure()) return res.Miss();

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
        Result res = ResolvePath(ref fullPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        res = _baseFs.OpenDirectory(ref outDirectory, in fullPath, mode);
        if (res.IsFailure()) return res.Miss();

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
        Result res = _baseFs.DeleteDirectoryRecursively(destPath);

        // Changed: Nintendo returns all errors unconditionally because SynchronizeDirectory is always called 
        // in situations where a PathNotFound error would mean the save directory was in an invalid state.
        // We'll ignore PathNotFound errors to be more user-friendly to users who might accidentally
        // put the save directory in an invalid state.
        if (res.IsFailure() && !ResultFs.PathNotFound.Includes(res)) return res;

        res = _baseFs.CreateDirectory(destPath);
        if (res.IsFailure()) return res.Miss();

        var directoryEntry = new DirectoryEntry();

        // Changed: Lock only if initialized with a client
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

    private Result DoCommit(bool updateTimeStamp)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        if (!_isJournalingEnabled || !_isJournalingSupported)
            return Result.Success;

        using var closure = new RetryClosure(this);

        Result res = PathFunctions.SetUpFixedPath(ref closure.ModifiedPath.Ref(), ModifiedDirectoryName);
        if (res.IsFailure()) return res.Miss();

        res = PathFunctions.SetUpFixedPath(ref closure.CommittedPath.Ref(), CommittedDirectoryName);
        if (res.IsFailure()) return res.Miss();

        res = PathFunctions.SetUpFixedPath(ref closure.SynchronizingPath.Ref(), SynchronizingDirectoryName);
        if (res.IsFailure()) return res.Miss();

        // All files must be closed before commiting save data.
        if (_openWritableFileCount > 0)
            return ResultFs.WriteModeFileNotClosed.Log();

        // Get rid of the previous commit by renaming the folder.
        res = RetryFinitelyForTargetLocked(in closure,
            (in RetryClosure c) => c.This._baseFs.RenameDirectory(in c.CommittedPath, in c.SynchronizingPath));
        if (res.IsFailure()) return res.Miss();

        // If something goes wrong beyond this point, the commit of the main data
        // will be completed the next time the savedata is opened.

        if (updateTimeStamp && _timeStampGetter is not null)
        {
            Assert.SdkNotNull(_randomGenerator);

            res = UpdateExtraDataTimeStamp();
            if (res.IsFailure()) return res.Miss();
        }

        res = CommitExtraDataImpl();
        if (res.IsFailure()) return res.Miss();

        res = RetryFinitelyForTargetLocked(in closure,
            (in RetryClosure c) => c.This.SynchronizeDirectory(in c.SynchronizingPath, in c.ModifiedPath));
        if (res.IsFailure()) return res.Miss();

        res = RetryFinitelyForTargetLocked(in closure,
            (in RetryClosure c) => c.This._baseFs.RenameDirectory(in c.SynchronizingPath, in c.CommittedPath));
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoCommit()
    {
        Result res = DoCommit(updateTimeStamp: true);
        if (res.IsFailure()) return res.Miss();

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
        // No old data is kept for non-journaling save data, so there's nothing to rollback to in that case
        if (_isJournalingSupported)
        {
            Result res = Initialize(_isJournalingSupported, _isMultiCommitSupported, _isJournalingEnabled,
                _timeStampGetter, _randomGenerator);
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out freeSpace);

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        using var pathModifiedDirectory = new Path();
        Result res = PathFunctions.SetUpFixedPath(ref pathModifiedDirectory.Ref(), ModifiedDirectoryName);
        if (res.IsFailure()) return res.Miss();

        res = _baseFs.GetFreeSpaceSize(out freeSpace, in pathModifiedDirectory);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out totalSpace);

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        using var pathModifiedDirectory = new Path();
        Result res = PathFunctions.SetUpFixedPath(ref pathModifiedDirectory.Ref(), ModifiedDirectoryName);
        if (res.IsFailure()) return res.Miss();

        res = _baseFs.GetTotalSpaceSize(out totalSpace, in pathModifiedDirectory);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public override bool IsSaveDataFileSystemCacheEnabled()
    {
        return false;
    }

    public override Result RollbackOnlyModified()
    {
        return ResultFs.UnsupportedRollbackOnlyModifiedForDirectorySaveDataFileSystem.Log();
    }

    public override Result WriteExtraData(in SaveDataExtraData extraData)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        return WriteExtraDataImpl(in extraData);
    }

    public override Result CommitExtraData(bool updateTimeStamp)
    {
        Result res = DoCommit(updateTimeStamp);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public override Result ReadExtraData(out SaveDataExtraData extraData)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        return ReadExtraDataImpl(out extraData);
    }

    public override void RegisterExtraDataAccessorObserver(ISaveDataExtraDataAccessorObserver observer,
        SaveDataSpaceId spaceId, ulong saveDataId)
    {
        _cacheObserver = observer;
        _spaceId = spaceId;
        _saveDataId = saveDataId;
    }

    private void DecrementWriteOpenFileCount()
    {
        // Todo?: Calling OpenFile when outFile already contains a DirectorySaveDataFile
        // will try to lock this mutex a second time
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        _openWritableFileCount--;
    }

    // The original class doesn't support transactional extra data,
    // always writing the extra data directly to the /extradata file.
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

    /// <summary>
    /// Initializes the save data's extra data files.
    /// </summary>
    /// <returns></returns>
    /// <remarks><para>There's no telling what state users might leave the extra data files in, so we want
    /// to be able to handle or recover from all possible states based on which files exist:</para>
    /// <para>This is the state a properly committed save should be in.<br/>
    /// Committed, Modified -> Use committed</para>
    /// <para>This is the state the save will be in after an interrupted commit.<br/>
    /// Working, Synchronizing -> Use modified</para>
    /// <para>These states shouldn't normally happen. Use the committed file, ignoring the others.<br/>
    /// Committed, Synchronizing -> Use committed<br/>
    /// Committed, Modified, Synchronizing -> Use committed</para>
    /// <para>If only one file exists then use that file.<br/>
    /// Committed -> Use committed<br/>
    /// Modified -> Use modified<br/>
    /// Synchronizing -> Use synchronizing</para>
    /// </remarks>
    private Result InitializeExtraData()
    {
        using var pathModifiedExtraData = new Path();
        Result res = PathFunctions.SetUpFixedPath(ref pathModifiedExtraData.Ref(), ModifiedExtraDataName);
        if (res.IsFailure()) return res.Miss();

        using var pathCommittedExtraData = new Path();
        res = PathFunctions.SetUpFixedPath(ref pathCommittedExtraData.Ref(), CommittedExtraDataName);
        if (res.IsFailure()) return res.Miss();

        using var pathSynchronizingExtraData = new Path();
        res = PathFunctions.SetUpFixedPath(ref pathSynchronizingExtraData.Ref(), SynchronizingExtraDataName);
        if (res.IsFailure()) return res.Miss();

        // Ensure the extra data files exist.
        // We don't currently handle the case where some of the extra data paths are directories instead of files.
        res = _baseFs.GetEntryType(out _, in pathModifiedExtraData);

        if (res.IsFailure())
        {
            if (!ResultFs.PathNotFound.Includes(res))
                return res;

            // The Modified file doesn't exist. Create it.
            res = _baseFs.CreateFile(in pathModifiedExtraData, Unsafe.SizeOf<SaveDataExtraData>());
            if (res.IsFailure()) return res.Miss();

            if (_isJournalingSupported)
            {
                res = _baseFs.GetEntryType(out _, in pathCommittedExtraData);

                if (res.IsFailure())
                {
                    if (!ResultFs.PathNotFound.Includes(res))
                        return res;

                    // Neither the modified or committed files existed.
                    // Check if the synchronizing file exists and use it if it does.
                    res = _baseFs.GetEntryType(out _, in pathSynchronizingExtraData);

                    if (res.IsSuccess())
                    {
                        res = _baseFs.RenameFile(in pathSynchronizingExtraData, in pathCommittedExtraData);
                        if (res.IsFailure()) return res.Miss();
                    }
                    else
                    {
                        // The synchronizing file did not exist. Create an empty committed extra data file.
                        res = _baseFs.CreateFile(in pathCommittedExtraData, Unsafe.SizeOf<SaveDataExtraData>());
                        if (res.IsFailure() && !ResultFs.PathAlreadyExists.Includes(res))
                            return res;
                    }
                }
            }
        }
        else
        {
            // If the working file exists make sure it's the right size
            res = EnsureExtraDataSize(in pathModifiedExtraData);
            if (res.IsFailure()) return res.Miss();
        }

        // Only the working extra data is needed for non-journaling savedata
        if (_isJournalingSupported)
        {
            res = _baseFs.GetEntryType(out _, in pathCommittedExtraData);

            if (res.IsSuccess())
            {
                res = EnsureExtraDataSize(in pathCommittedExtraData);
                if (res.IsFailure()) return res.Miss();

                if (_isJournalingEnabled)
                {
                    res = SynchronizeExtraData(in pathModifiedExtraData, in pathCommittedExtraData);
                    if (res.IsFailure()) return res.Miss();
                }
            }
            else if (ResultFs.PathNotFound.Includes(res))
            {
                // The committed file doesn't exist. Try to recover from whatever invalid state we're in.

                // If the synchronizing file exists then the previous commit failed.
                // Finish that commit by copying the working extra data to the committed extra data
                if (_baseFs.GetEntryType(out _, in pathSynchronizingExtraData).IsSuccess())
                {
                    res = SynchronizeExtraData(in pathSynchronizingExtraData, in pathModifiedExtraData);
                    if (res.IsFailure()) return res.Miss();

                    res = _baseFs.RenameFile(in pathSynchronizingExtraData, in pathCommittedExtraData);
                    if (res.IsFailure()) return res.Miss();
                }
                else
                {
                    // The only existing file is the modified file.
                    // Copy the working extra data to the committed extra data.
                    res = _baseFs.CreateFile(in pathSynchronizingExtraData, Unsafe.SizeOf<SaveDataExtraData>());
                    if (res.IsFailure()) return res.Miss();

                    res = SynchronizeExtraData(in pathSynchronizingExtraData, in pathModifiedExtraData);
                    if (res.IsFailure()) return res.Miss();

                    res = _baseFs.RenameFile(in pathSynchronizingExtraData, in pathCommittedExtraData);
                    if (res.IsFailure()) return res.Miss();
                }
            }
            else
            {
                return res;
            }
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

    private Result EnsureExtraDataSize(in Path path)
    {
        using var file = new UniqueRef<IFile>();
        Result res = _baseFs.OpenFile(ref file.Ref(), in path, OpenMode.ReadWrite);
        if (res.IsFailure()) return res.Miss();

        res = file.Get.GetSize(out long fileSize);
        if (res.IsFailure()) return res.Miss();

        if (fileSize == Unsafe.SizeOf<SaveDataExtraData>())
            return Result.Success;

        return file.Get.SetSize(Unsafe.SizeOf<SaveDataExtraData>());
    }

    private Result SynchronizeExtraData(in Path destPath, in Path sourcePath)
    {
        Span<byte> workBuffer = stackalloc byte[Unsafe.SizeOf<SaveDataExtraData>()];

        using (var sourceFile = new UniqueRef<IFile>())
        {
            Result res = _baseFs.OpenFile(ref sourceFile.Ref(), in sourcePath, OpenMode.Read);
            if (res.IsFailure()) return res.Miss();

            res = sourceFile.Get.Read(out long bytesRead, 0, workBuffer);
            if (res.IsFailure()) return res.Miss();

            Assert.SdkEqual(bytesRead, Unsafe.SizeOf<SaveDataExtraData>());
        }

        using (var destFile = new UniqueRef<IFile>())
        {
            Result res = _baseFs.OpenFile(ref destFile.Ref(), in destPath, OpenMode.Write);
            if (res.IsFailure()) return res.Miss();

            res = destFile.Get.Write(0, workBuffer, WriteOption.Flush);
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    private Result UpdateExtraDataTimeStamp()
    {
        Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

        Result res = ReadExtraDataImpl(out SaveDataExtraData extraData);
        if (res.IsFailure()) return res.Miss();

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
        Result res = GetExtraDataPath(ref pathExtraData.Ref());
        if (res.IsFailure()) return res.Miss();

        using var file = new UniqueRef<IFile>();
        res = _baseFs.OpenFile(ref file.Ref(), in pathExtraData, OpenMode.Write);
        if (res.IsFailure()) return res.Miss();

        res = file.Get.Write(0, SpanHelpers.AsReadOnlyByteSpan(in extraData), WriteOption.Flush);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private Result CommitExtraDataImpl()
    {
        Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

        if (!_isJournalingSupported || !_isJournalingEnabled)
            return Result.Success;

        using var closure = new RetryClosure(this);

        Result res = PathFunctions.SetUpFixedPath(ref closure.ModifiedPath.Ref(), ModifiedExtraDataName);
        if (res.IsFailure()) return res.Miss();

        res = PathFunctions.SetUpFixedPath(ref closure.CommittedPath.Ref(), CommittedExtraDataName);
        if (res.IsFailure()) return res.Miss();

        res = PathFunctions.SetUpFixedPath(ref closure.SynchronizingPath.Ref(), SynchronizingExtraDataName);
        if (res.IsFailure()) return res.Miss();

        // Get rid of the previous commit by renaming the file.
        res = RetryFinitelyForTargetLocked(in closure,
            (in RetryClosure c) => c.This._baseFs.RenameFile(in c.CommittedPath, in c.SynchronizingPath));
        if (res.IsFailure()) return res.Miss();

        // If something goes wrong beyond this point, the commit will be
        // completed the next time the savedata is opened.

        res = RetryFinitelyForTargetLocked(in closure,
            (in RetryClosure c) => c.This.SynchronizeExtraData(in c.SynchronizingPath, in c.ModifiedPath));
        if (res.IsFailure()) return res.Miss();

        res = RetryFinitelyForTargetLocked(in closure,
            (in RetryClosure c) => c.This._baseFs.RenameFile(in c.SynchronizingPath, in c.CommittedPath));
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private Result ReadExtraDataImpl(out SaveDataExtraData extraData)
    {
        UnsafeHelpers.SkipParamInit(out extraData);

        Assert.SdkRequires(_mutex.IsLockedByCurrentThread());

        using var pathExtraData = new Path();
        Result res = GetExtraDataPath(ref pathExtraData.Ref());
        if (res.IsFailure()) return res.Miss();

        using var file = new UniqueRef<IFile>();
        res = _baseFs.OpenFile(ref file.Ref(), in pathExtraData, OpenMode.Read);
        if (res.IsFailure()) return res.Miss();

        res = file.Get.Read(out long bytesRead, 0, SpanHelpers.AsByteSpan(ref extraData));
        if (res.IsFailure()) return res.Miss();

        Assert.SdkEqual(bytesRead, Unsafe.SizeOf<SaveDataExtraData>());

        return Result.Success;
    }

    public SaveDataSpaceId GetSaveDataSpaceId() => _spaceId;
    public ulong GetSaveDataId() => _saveDataId;
}