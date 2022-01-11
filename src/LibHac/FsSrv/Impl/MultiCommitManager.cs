using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using LibHac.Sf;

using IFile = LibHac.Fs.Fsa.IFile;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.FsSrv.Impl;

internal struct MultiCommitManagerGlobals
{
    public SdkMutexType MultiCommitMutex;

    public void Initialize()
    {
        MultiCommitMutex.Initialize();
    }
}

/// <summary>
/// Manages atomically committing a group of file systems.
/// </summary>
/// <remarks>
/// The commit process is as follows:<br/>
/// 1. Create a commit context file that tracks the progress of the commit in case it is interrupted.<br/>
/// 2. Provisionally commit each file system individually. If any fail, rollback the file systems that were provisionally committed.<br/>
/// 3. Update the commit context file to note that the file systems have been provisionally committed.
/// If the multi-commit is interrupted past this point, the file systems will be fully committed during recovery.<br/>
/// 4. Fully commit each file system individually.<br/>
/// 5. Delete the commit context file.<br/>
///<br/>
/// Even though multi-commits are supposed to be atomic, issues can arise from errors during the process of fully committing the save data.
/// Save data image files are designed so that minimal changes are made when fully committing a provisionally committed save.
/// However if any commit fails for any reason, all other saves in the multi-commit will still be committed.
/// This can especially cause issues with directory save data where finishing a commit is much more involved.
/// <para>Based on FS 12.1.0 (nnSdk 12.3.1)</para>
/// </remarks>
internal class MultiCommitManager : IMultiCommitManager
{
    private const int MaxFileSystemCount = 10;

    public const ulong ProgramId = 0x0100000000000000;
    public const ulong SaveDataId = 0x8000000000000001;
    private const long SaveDataSize = 0xC000;
    private const long SaveJournalSize = 0xC000;

    private const int CurrentCommitContextVersion = 0x10000;
    private const long CommitContextFileSize = 0x200;

    /// <summary>"<c>/commitinfo</c>"</summary>
    private static ReadOnlySpan<byte> CommitContextFileName =>
        new[] { (byte)'/', (byte)'c', (byte)'o', (byte)'m', (byte)'m', (byte)'i', (byte)'t', (byte)'i', (byte)'n', (byte)'f', (byte)'o' };

    private SharedRef<ISaveDataMultiCommitCoreInterface> _multiCommitInterface;
    private readonly SharedRef<IFileSystem>[] _fileSystems;
    private int _fileSystemCount;
    private long _counter;

    // LibHac additions
    private readonly FileSystemServer _fsServer;
    private ref MultiCommitManagerGlobals Globals => ref _fsServer.Globals.MultiCommitManager;

    public MultiCommitManager(FileSystemServer fsServer, ref SharedRef<ISaveDataMultiCommitCoreInterface> multiCommitInterface)
    {
        _fsServer = fsServer;

        _multiCommitInterface = SharedRef<ISaveDataMultiCommitCoreInterface>.CreateMove(ref multiCommitInterface);
        _fileSystems = new SharedRef<IFileSystem>[MaxFileSystemCount];
        _fileSystemCount = 0;
        _counter = 0;
    }

    public static SharedRef<IMultiCommitManager> CreateShared(FileSystemServer fsServer,
        ref SharedRef<ISaveDataMultiCommitCoreInterface> multiCommitInterface)
    {
        return new SharedRef<IMultiCommitManager>(new MultiCommitManager(fsServer, ref multiCommitInterface));
    }

    public void Dispose()
    {
        for (int i = 0; i < _fileSystems.Length; i++)
        {
            _fileSystems[i].Destroy();
        }

        _multiCommitInterface.Destroy();
    }

    /// <summary>
    /// Ensures the save data used to store the commit context exists.
    /// </summary>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    private Result EnsureSaveDataForContext()
    {
        using var contextFileSystem = new SharedRef<IFileSystem>();
        Result rc = _multiCommitInterface.Get.OpenMultiCommitContext(ref contextFileSystem.Ref());

        if (rc.IsFailure())
        {
            if (!ResultFs.TargetNotFound.Includes(rc))
                return rc;

            rc = _fsServer.Hos.Fs.CreateSystemSaveData(SaveDataId, SaveDataSize, SaveJournalSize, SaveDataFlags.None);
            if (rc.IsFailure()) return rc;
        }

        return Result.Success;
    }

    /// <summary>
    /// Adds a file system to the list of file systems to be committed.
    /// </summary>
    /// <param name="fileSystem">The file system to be committed.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.MultiCommitFileSystemLimit"/>: The maximum number of file systems have been added.
    /// <see cref="MaxFileSystemCount"/> file systems may be added to a single multi-commit.<br/>
    /// <see cref="ResultFs.MultiCommitHasOverlappingTargets"/>: The provided file system has already been added.</returns>
    public Result Add(ref SharedRef<IFileSystemSf> fileSystem)
    {
        if (_fileSystemCount >= MaxFileSystemCount)
            return ResultFs.MultiCommitFileSystemLimit.Log();

        using var fsaFileSystem = new SharedRef<IFileSystem>();
        Result rc = fileSystem.Get.GetImpl(ref fsaFileSystem.Ref());
        if (rc.IsFailure()) return rc;

        // Check that the file system hasn't already been added
        for (int i = 0; i < _fileSystemCount; i++)
        {
            if (ReferenceEquals(fsaFileSystem.Get, _fileSystems[i].Get))
                return ResultFs.MultiCommitHasOverlappingTargets.Log();
        }

        _fileSystems[_fileSystemCount].SetByMove(ref fsaFileSystem.Ref());
        _fileSystemCount++;

        return Result.Success;
    }

    /// <summary>
    /// Commits all added file systems using <paramref name="contextFileSystem"/> to
    /// store the <see cref="Context"/>.
    /// </summary>
    /// <param name="contextFileSystem">The file system where the commit context will be stored.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    private Result Commit(IFileSystem contextFileSystem)
    {
        _counter = 1;

        using var contextUpdater = new ContextUpdater(contextFileSystem);
        Result rc = contextUpdater.Create(_counter, _fileSystemCount);
        if (rc.IsFailure()) return rc;

        rc = CommitProvisionallyFileSystem(_counter);
        if (rc.IsFailure()) return rc;

        rc = contextUpdater.CommitProvisionallyDone();
        if (rc.IsFailure()) return rc;

        rc = CommitFileSystem();
        if (rc.IsFailure()) return rc;

        rc = contextUpdater.CommitDone();
        if (rc.IsFailure()) return rc;

        return Result.Success;
    }

    /// <summary>
    /// Commits all added file systems.
    /// </summary>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public Result Commit()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref Globals.MultiCommitMutex);

        using var contextFileSystem = new SharedRef<IFileSystem>();
        Result rc = EnsureSaveDataForContext();
        if (rc.IsFailure()) return rc;

        rc = _multiCommitInterface.Get.OpenMultiCommitContext(ref contextFileSystem.Ref());
        if (rc.IsFailure()) return rc;

        return Commit(contextFileSystem.Get);
    }

    /// <summary>
    /// Tries to provisionally commit all the added file systems.
    /// </summary>
    /// <param name="counter">The provisional commit counter.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    private Result CommitProvisionallyFileSystem(long counter)
    {
        Result rc = Result.Success;
        int i;

        for (i = 0; i < _fileSystemCount; i++)
        {
            Assert.SdkNotNull(_fileSystems[i].Get);

            rc = _fileSystems[i].Get.CommitProvisionally(counter);

            if (rc.IsFailure())
                break;
        }

        if (rc.IsFailure())
        {
            // Rollback all provisional commits including the failed commit
            for (int j = 0; j <= i; j++)
            {
                Assert.SdkNotNull(_fileSystems[j].Get);

                _fileSystems[j].Get.Rollback().IgnoreResult();
            }
        }

        return rc;
    }

    /// <summary>
    /// Tries to fully commit all the added file systems.
    /// </summary>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    private Result CommitFileSystem()
    {
        // Try to commit all file systems even if one fails.
        // If any commits fail, the result from the first failed recovery will be returned.
        Result result = Result.Success;

        for (int i = 0; i < _fileSystemCount; i++)
        {
            Assert.SdkNotNull(_fileSystems[i].Get);

            Result resultLast = _fileSystems[i].Get.Commit();

            // If the commit failed, set the overall result if it hasn't been set yet.
            if (result.IsSuccess() && resultLast.IsFailure())
            {
                result = resultLast;
            }
        }

        if (result.IsFailure()) return result.Miss();

        return Result.Success;
    }

    /// <summary>
    /// Recovers a multi-commit that was interrupted after all file systems had been provisionally committed.
    /// The recovery will finish committing any file systems that are still provisionally committed.
    /// </summary>
    /// <param name="multiCommitInterface">The core interface used for multi-commits.</param>
    /// <param name="contextFs">The file system containing the multi-commit context file.</param>
    /// <param name="saveService">The save data service.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidMultiCommitContextVersion"/>: The version of the commit context
    /// file isn't supported.<br/>
    /// <see cref="ResultFs.InvalidMultiCommitContextState"/>: The multi-commit hadn't finished
    /// provisionally committing all the file systems.</returns>
    private static Result RecoverCommit(ISaveDataMultiCommitCoreInterface multiCommitInterface,
        IFileSystem contextFs, SaveDataFileSystemServiceImpl saveService)
    {
        using var contextFilePath = new Fs.Path();
        Result rc = PathFunctions.SetUpFixedPath(ref contextFilePath.Ref(), CommitContextFileName);
        if (rc.IsFailure()) return rc;

        // Read the multi-commit context
        using var contextFile = new UniqueRef<IFile>();
        rc = contextFs.OpenFile(ref contextFile.Ref(), in contextFilePath, OpenMode.ReadWrite);
        if (rc.IsFailure()) return rc;

        Unsafe.SkipInit(out Context context);
        rc = contextFile.Get.Read(out _, 0, SpanHelpers.AsByteSpan(ref context), ReadOption.None);
        if (rc.IsFailure()) return rc;

        // Note: Nintendo doesn't check if the proper amount of bytes were read, but it
        // doesn't really matter since the context is validated.
        if (context.Version > CurrentCommitContextVersion)
            return ResultFs.InvalidMultiCommitContextVersion.Log();

        // All the file systems in the multi-commit must have been at least provisionally committed
        // before we can try to recover the commit.
        if (context.State != CommitState.ProvisionallyCommitted)
            return ResultFs.InvalidMultiCommitContextState.Log();

        // Keep track of the first error that occurs during the recovery
        Result recoveryResult = Result.Success;

        int saveCount = 0;
        Span<SaveDataInfo> savesToRecover = stackalloc SaveDataInfo[MaxFileSystemCount];

        {
            using var reader = new SharedRef<SaveDataInfoReaderImpl>();
            using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

            rc = saveService.OpenSaveDataIndexerAccessor(ref accessor.Ref(), out _, SaveDataSpaceId.User);
            if (rc.IsFailure()) return rc;

            rc = accessor.Get.Indexer.OpenSaveDataInfoReader(ref reader.Ref());
            if (rc.IsFailure()) return rc;

            // Iterate through all the saves to find any provisionally committed save data
            while (true)
            {
                Unsafe.SkipInit(out SaveDataInfo info);

                rc = reader.Get.Read(out long readCount, OutBuffer.FromStruct(ref info));
                if (rc.IsFailure()) return rc;

                // Break once we're done iterating all save data
                if (readCount == 0)
                    break;

                rc = multiCommitInterface.IsProvisionallyCommittedSaveData(out bool isProvisionallyCommitted,
                    in info);

                // Note: Multi-commits are only recovered at boot time, so some saves could be missed if there
                // are more than MaxFileSystemCount provisionally committed saves.
                // In theory this shouldn't happen because a multi-commit should only be interrupted if the
                // entire OS is brought down.
                if (rc.IsSuccess() && isProvisionallyCommitted && saveCount < MaxFileSystemCount)
                {
                    savesToRecover[saveCount] = info;
                    saveCount++;
                }
            }
        }

        // Recover the saves by finishing their commits.
        // All file systems will try to be recovered, even if one fails.
        // If any commits fail, the result from the first failed recovery will be returned.
        for (int i = 0; i < saveCount; i++)
        {
            rc = multiCommitInterface.RecoverProvisionallyCommittedSaveData(in savesToRecover[i], false);

            if (rc.IsFailure() && !recoveryResult.IsFailure())
            {
                recoveryResult = rc;
            }
        }

        return recoveryResult;
    }

    /// <summary>
    /// Tries to recover a multi-commit using the context in the provided file system.
    /// </summary>
    /// <param name="multiCommitInterface">The core interface used for multi-commits.</param>
    /// <param name="contextFs">The file system containing the multi-commit context file.</param>
    /// <param name="saveService">The save data service.</param>
    /// <returns></returns>
    private static Result Recover(ISaveDataMultiCommitCoreInterface multiCommitInterface, IFileSystem contextFs,
        SaveDataFileSystemServiceImpl saveService)
    {
        if (multiCommitInterface is null)
            return ResultFs.InvalidArgument.Log();

        if (contextFs is null)
            return ResultFs.InvalidArgument.Log();

        // Keep track of the first error that occurs during the recovery
        Result recoveryResult = Result.Success;

        Result rc = RecoverCommit(multiCommitInterface, contextFs, saveService);

        if (rc.IsFailure())
        {
            // Note: Yes, the next ~50 lines are exactly the same as the code in RecoverCommit except
            // for a single bool value. No, Nintendo doesn't split it out into its own function.
            int saveCount = 0;
            Span<SaveDataInfo> savesToRecover = stackalloc SaveDataInfo[MaxFileSystemCount];

            {
                using var reader = new SharedRef<SaveDataInfoReaderImpl>();
                using var accessor = new UniqueRef<SaveDataIndexerAccessor>();

                rc = saveService.OpenSaveDataIndexerAccessor(ref accessor.Ref(), out _, SaveDataSpaceId.User);
                if (rc.IsFailure()) return rc;

                rc = accessor.Get.Indexer.OpenSaveDataInfoReader(ref reader.Ref());
                if (rc.IsFailure()) return rc;

                // Iterate through all the saves to find any provisionally committed save data
                while (true)
                {
                    Unsafe.SkipInit(out SaveDataInfo info);

                    rc = reader.Get.Read(out long readCount, OutBuffer.FromStruct(ref info));
                    if (rc.IsFailure()) return rc;

                    // Break once we're done iterating all save data
                    if (readCount == 0)
                        break;

                    rc = multiCommitInterface.IsProvisionallyCommittedSaveData(out bool isProvisionallyCommitted,
                        in info);

                    // Note: Multi-commits are only recovered at boot time, so some saves could be missed if there
                    // are more than MaxFileSystemCount provisionally committed saves.
                    // In theory this shouldn't happen because a multi-commit should only be interrupted if the
                    // entire OS is brought down.
                    if (rc.IsSuccess() && isProvisionallyCommitted && saveCount < MaxFileSystemCount)
                    {
                        savesToRecover[saveCount] = info;
                        saveCount++;
                    }
                }
            }

            // Recover the saves by rolling them back to the previous commit.
            // All file systems will try to be recovered, even if one fails.
            // If any commits fail, the result from the first failed recovery will be returned.
            for (int i = 0; i < saveCount; i++)
            {
                rc = multiCommitInterface.RecoverProvisionallyCommittedSaveData(in savesToRecover[i], true);

                if (rc.IsFailure() && !recoveryResult.IsFailure())
                {
                    recoveryResult = rc;
                }
            }
        }

        using var contextFilePath = new Fs.Path();
        rc = PathFunctions.SetUpFixedPath(ref contextFilePath.Ref(), CommitContextFileName);
        if (rc.IsFailure()) return rc;

        // Delete the commit context file
        rc = contextFs.DeleteFile(in contextFilePath);
        if (rc.IsFailure()) return rc;

        rc = contextFs.Commit();
        if (rc.IsFailure()) return rc;

        return recoveryResult;
    }

    /// <summary>
    /// Recovers an interrupted multi-commit. The commit will either be completed or rolled back depending on
    /// where in the commit process it was interrupted. Does nothing if there is no commit to recover.
    /// </summary>
    /// <param name="fsServer">The <see cref="FileSystemServer"/> that contains the save data to recover.</param>
    /// <param name="multiCommitInterface">The core interface used for multi-commits.</param>
    /// <param name="saveService">The save data service.</param>
    /// <returns>The <see cref="Result"/> of the operation.<br/>
    /// <see cref="Result.Success"/>: The recovery was successful or there was no multi-commit to recover.</returns>
    public static Result Recover(FileSystemServer fsServer, ISaveDataMultiCommitCoreInterface multiCommitInterface,
        SaveDataFileSystemServiceImpl saveService)
    {
        using ScopedLock<SdkMutexType> scopedLock =
            ScopedLock.Lock(ref fsServer.Globals.MultiCommitManager.MultiCommitMutex);

        bool needsRecovery = true;
        using var fileSystem = new SharedRef<IFileSystem>();

        // Check if a multi-commit was interrupted by checking if there's a commit context file.
        Result rc = multiCommitInterface.OpenMultiCommitContext(ref fileSystem.Ref());

        if (rc.IsFailure())
        {
            if (!ResultFs.PathNotFound.Includes(rc) && !ResultFs.TargetNotFound.Includes(rc))
                return rc;

            // Unable to open the multi-commit context file system, so there's nothing to recover
            needsRecovery = false;
        }

        if (needsRecovery)
        {
            using var contextFilePath = new Fs.Path();
            rc = PathFunctions.SetUpFixedPath(ref contextFilePath.Ref(), CommitContextFileName);
            if (rc.IsFailure()) return rc;

            using var file = new UniqueRef<IFile>();
            rc = fileSystem.Get.OpenFile(ref file.Ref(), in contextFilePath, OpenMode.Read);

            if (rc.IsFailure())
            {
                // Unable to open the context file. No multi-commit to recover.
                if (ResultFs.PathNotFound.Includes(rc))
                    needsRecovery = false;
            }
        }

        if (!needsRecovery)
            return Result.Success;

        // There was a context file. Recover the unfinished commit.
        return Recover(multiCommitInterface, fileSystem.Get, saveService);
    }

    private struct Context
    {
        public int Version;
        public CommitState State;
        public int FileSystemCount;
        public long Counter;
    }

    private enum CommitState
    {
        // ReSharper disable once UnusedMember.Local
        None = 0,
        NotCommitted = 1,
        ProvisionallyCommitted = 2
    }

    private struct ContextUpdater : IDisposable
    {
        private Context _context;
        private IFileSystem _fileSystem;

        public ContextUpdater(IFileSystem contextFileSystem)
        {
            _context = default;
            _fileSystem = contextFileSystem;
        }

        public void Dispose()
        {
            if (_fileSystem is null) return;

            using var contextFilePath = new Fs.Path();
            PathFunctions.SetUpFixedPath(ref contextFilePath.Ref(), CommitContextFileName).IgnoreResult();
            _fileSystem.DeleteFile(in contextFilePath).IgnoreResult();
            _fileSystem.Commit().IgnoreResult();

            _fileSystem = null;
        }

        /// <summary>
        /// Creates and writes the initial commit context to a file.
        /// </summary>
        /// <param name="counter">The counter.</param>
        /// <param name="fileSystemCount">The number of file systems being committed.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result Create(long counter, int fileSystemCount)
        {
            using var contextFilePath = new Fs.Path();
            Result rc = PathFunctions.SetUpFixedPath(ref contextFilePath.Ref(), CommitContextFileName);
            if (rc.IsFailure()) return rc;

            // Open context file and create if it doesn't exist
            using (var contextFile = new UniqueRef<IFile>())
            {
                rc = _fileSystem.OpenFile(ref contextFile.Ref(), in contextFilePath, OpenMode.Read);

                if (rc.IsFailure())
                {
                    if (!ResultFs.PathNotFound.Includes(rc))
                        return rc;

                    rc = _fileSystem.CreateFile(in contextFilePath, CommitContextFileSize);
                    if (rc.IsFailure()) return rc;

                    rc = _fileSystem.OpenFile(ref contextFile.Ref(), in contextFilePath, OpenMode.Read);
                    if (rc.IsFailure()) return rc;
                }
            }

            using (var contextFile = new UniqueRef<IFile>())
            {
                rc = _fileSystem.OpenFile(ref contextFile.Ref(), in contextFilePath, OpenMode.ReadWrite);
                if (rc.IsFailure()) return rc;

                _context.Version = CurrentCommitContextVersion;
                _context.State = CommitState.NotCommitted;
                _context.FileSystemCount = fileSystemCount;
                _context.Counter = counter;

                // Write the initial context to the file
                rc = contextFile.Get.Write(0, SpanHelpers.AsByteSpan(ref _context), WriteOption.None);
                if (rc.IsFailure()) return rc;

                rc = contextFile.Get.Flush();
                if (rc.IsFailure()) return rc;
            }

            rc = _fileSystem.Commit();
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        /// <summary>
        /// Updates the commit context and writes it to a file, signifying that all
        /// the file systems have been provisionally committed.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result CommitProvisionallyDone()
        {
            using (var contextFilePath = new Fs.Path())
            {
                Result rc = PathFunctions.SetUpFixedPath(ref contextFilePath.Ref(), CommitContextFileName);
                if (rc.IsFailure()) return rc;

                using var contextFile = new UniqueRef<IFile>();
                rc = _fileSystem.OpenFile(ref contextFile.Ref(), in contextFilePath, OpenMode.ReadWrite);
                if (rc.IsFailure()) return rc;

                _context.State = CommitState.ProvisionallyCommitted;

                rc = contextFile.Get.Write(0, SpanHelpers.AsByteSpan(ref _context), WriteOption.None);
                if (rc.IsFailure()) return rc;

                rc = contextFile.Get.Flush();
                if (rc.IsFailure()) return rc;
            }

            return _fileSystem.Commit();
        }

        /// <summary>
        /// To be called once the multi-commit has been successfully completed. Deletes the commit context file.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result CommitDone()
        {
            using var contextFilePath = new Fs.Path();
            Result rc = PathFunctions.SetUpFixedPath(ref contextFilePath.Ref(), CommitContextFileName);
            if (rc.IsFailure()) return rc;

            rc = _fileSystem.DeleteFile(in contextFilePath);
            if (rc.IsFailure()) return rc;

            rc = _fileSystem.Commit();
            if (rc.IsFailure()) return rc;

            _fileSystem = null;
            return Result.Success;
        }
    }
}