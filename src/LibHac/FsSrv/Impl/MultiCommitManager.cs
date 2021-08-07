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

namespace LibHac.FsSrv.Impl
{
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
    /// This can especially cause issues with directory save data where finishing a commit is much more involved.<br/>
    /// <br/>
    /// Based on FS 12.0.3 (nnSdk 12.3.1)
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

        // /commitinfo
        private static ReadOnlySpan<byte> CommitContextFileName =>
            new[] { (byte)'/', (byte)'c', (byte)'o', (byte)'m', (byte)'m', (byte)'i', (byte)'t', (byte)'i', (byte)'n', (byte)'f', (byte)'o' };

        private ReferenceCountedDisposable<ISaveDataMultiCommitCoreInterface> _multiCommitInterface;
        private readonly ReferenceCountedDisposable<IFileSystem>[] _fileSystems;
        private int _fileSystemCount;
        private long _counter;

        // Extra field used in LibHac
        private readonly FileSystemServer _fsServer;
        private ref MultiCommitManagerGlobals Globals => ref _fsServer.Globals.MultiCommitManager;

        public MultiCommitManager(FileSystemServer fsServer, ref ReferenceCountedDisposable<ISaveDataMultiCommitCoreInterface> multiCommitInterface)
        {
            _fsServer = fsServer;

            _multiCommitInterface = Shared.Move(ref multiCommitInterface);
            _fileSystems = new ReferenceCountedDisposable<IFileSystem>[MaxFileSystemCount];
            _fileSystemCount = 0;
            _counter = 0;
        }

        public static ReferenceCountedDisposable<IMultiCommitManager> CreateShared(FileSystemServer fsServer,
            ref ReferenceCountedDisposable<ISaveDataMultiCommitCoreInterface> multiCommitInterface)
        {
            var manager = new MultiCommitManager(fsServer, ref multiCommitInterface);
            return new ReferenceCountedDisposable<IMultiCommitManager>(manager);
        }

        public void Dispose()
        {
            foreach (ReferenceCountedDisposable<IFileSystem> fs in _fileSystems)
            {
                fs?.Dispose();
            }

            _multiCommitInterface?.Dispose();
        }

        /// <summary>
        /// Ensures the save data used to store the commit context exists.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private Result EnsureSaveDataForContext()
        {
            ReferenceCountedDisposable<IFileSystem> contextFs = null;
            try
            {
                Result rc = _multiCommitInterface.Target.OpenMultiCommitContext(out contextFs);

                if (rc.IsFailure())
                {
                    if (!ResultFs.TargetNotFound.Includes(rc))
                        return rc;

                    rc = _fsServer.Hos.Fs.CreateSystemSaveData(SaveDataId, SaveDataSize, SaveJournalSize, SaveDataFlags.None);
                    if (rc.IsFailure()) return rc;
                }

                return Result.Success;
            }
            finally
            {
                contextFs?.Dispose();
            }
        }

        /// <summary>
        /// Adds a file system to the list of file systems to be committed.
        /// </summary>
        /// <param name="fileSystem">The file system to be committed.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.MultiCommitFileSystemLimit"/>: The maximum number of file systems have been added.
        /// <see cref="MaxFileSystemCount"/> file systems may be added to a single multi-commit.<br/>
        /// <see cref="ResultFs.MultiCommitFileSystemAlreadyAdded"/>: The provided file system has already been added.</returns>
        public Result Add(ReferenceCountedDisposable<IFileSystemSf> fileSystem)
        {
            if (_fileSystemCount >= MaxFileSystemCount)
                return ResultFs.MultiCommitFileSystemLimit.Log();

            ReferenceCountedDisposable<IFileSystem> fsaFileSystem = null;
            try
            {
                Result rc = fileSystem.Target.GetImpl(out fsaFileSystem);
                if (rc.IsFailure()) return rc;

                // Check that the file system hasn't already been added
                for (int i = 0; i < _fileSystemCount; i++)
                {
                    if (ReferenceEquals(fsaFileSystem.Target, _fileSystems[i].Target))
                        return ResultFs.MultiCommitFileSystemAlreadyAdded.Log();
                }

                _fileSystems[_fileSystemCount] = Shared.Move(ref fsaFileSystem);
                _fileSystemCount++;

                return Result.Success;
            }
            finally
            {
                fsaFileSystem?.Dispose();
            }
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

            ReferenceCountedDisposable<IFileSystem> contextFs = null;
            try
            {
                Result rc = EnsureSaveDataForContext();
                if (rc.IsFailure()) return rc;

                rc = _multiCommitInterface.Target.OpenMultiCommitContext(out contextFs);
                if (rc.IsFailure()) return rc;

                return Commit(contextFs.Target);
            }
            finally
            {
                contextFs?.Dispose();
            }
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
                Assert.SdkNotNull(_fileSystems[i]);

                rc = _fileSystems[i].Target.CommitProvisionally(counter);

                if (rc.IsFailure())
                    break;
            }

            if (rc.IsFailure())
            {
                // Rollback all provisional commits including the failed commit
                for (int j = 0; j <= i; j++)
                {
                    Assert.SdkNotNull(_fileSystems[j]);

                    _fileSystems[j].Target.Rollback().IgnoreResult();
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
                Assert.SdkNotNull(_fileSystems[i]);

                Result rc = _fileSystems[i].Target.Commit();

                // If the commit failed, set the overall result if it hasn't been set yet.
                if (result.IsSuccess() && rc.IsFailure())
                {
                    result = rc;
                }
            }

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
            var contextFilePath = new Fs.Path();
            Result rc = PathFunctions.SetUpFixedPath(ref contextFilePath, CommitContextFileName);
            if (rc.IsFailure()) return rc;

            IFile contextFile = null;
            try
            {
                // Read the multi-commit context
                rc = contextFs.OpenFile(out contextFile, in contextFilePath, OpenMode.ReadWrite);
                if (rc.IsFailure()) return rc;

                Unsafe.SkipInit(out Context context);
                rc = contextFile.Read(out _, 0, SpanHelpers.AsByteSpan(ref context), ReadOption.None);
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

                SaveDataIndexerAccessor accessor = null;
                ReferenceCountedDisposable<SaveDataInfoReaderImpl> infoReader = null;
                try
                {
                    rc = saveService.OpenSaveDataIndexerAccessor(out accessor, out _, SaveDataSpaceId.User);
                    if (rc.IsFailure()) return rc;

                    rc = accessor.Indexer.OpenSaveDataInfoReader(out infoReader);
                    if (rc.IsFailure()) return rc;

                    // Iterate through all the saves to find any provisionally committed save data
                    while (true)
                    {
                        Unsafe.SkipInit(out SaveDataInfo info);

                        rc = infoReader.Target.Read(out long readCount, OutBuffer.FromStruct(ref info));
                        if (rc.IsFailure()) return rc;

                        // Break once we're done iterating all save data
                        if (readCount == 0)
                            break;

                        rc = multiCommitInterface.IsProvisionallyCommittedSaveData(out bool isProvisionallyCommitted,
                            in info);

                        // Note: Some saves could be missed if there are more than MaxFileSystemCount
                        // provisionally committed saves. Not sure why Nintendo doesn't catch this.
                        if (rc.IsSuccess() && isProvisionallyCommitted && saveCount < MaxFileSystemCount)
                        {
                            savesToRecover[saveCount] = info;
                            saveCount++;
                        }
                    }
                }
                finally
                {
                    accessor?.Dispose();
                    infoReader?.Dispose();
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

                contextFilePath.Dispose();
                return recoveryResult;
            }
            finally
            {
                contextFile?.Dispose();
            }
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

                SaveDataIndexerAccessor accessor = null;
                ReferenceCountedDisposable<SaveDataInfoReaderImpl> infoReader = null;
                try
                {
                    rc = saveService.OpenSaveDataIndexerAccessor(out accessor, out _, SaveDataSpaceId.User);
                    if (rc.IsFailure()) return rc;

                    rc = accessor.Indexer.OpenSaveDataInfoReader(out infoReader);
                    if (rc.IsFailure()) return rc;

                    // Iterate through all the saves to find any provisionally committed save data
                    while (true)
                    {
                        Unsafe.SkipInit(out SaveDataInfo info);

                        rc = infoReader.Target.Read(out long readCount, OutBuffer.FromStruct(ref info));
                        if (rc.IsFailure()) return rc;

                        // Break once we're done iterating all save data
                        if (readCount == 0)
                            break;

                        rc = multiCommitInterface.IsProvisionallyCommittedSaveData(out bool isProvisionallyCommitted,
                            in info);

                        // Note: Some saves could be missed if there are more than MaxFileSystemCount
                        // provisionally committed saves. Not sure why Nintendo doesn't catch this.
                        if (rc.IsSuccess() && isProvisionallyCommitted && saveCount < MaxFileSystemCount)
                        {
                            savesToRecover[saveCount] = info;
                            saveCount++;
                        }
                    }
                }
                finally
                {
                    accessor?.Dispose();
                    infoReader?.Dispose();
                }

                // Recover the saves by rolling them back to the previous commit.
                // All file systems will try to be recovered, even if one fails.
                // If any commits fail, the result from the first failed recovery will be returned.
                for (int i = 0; i < saveCount; i++)
                {
                    rc = multiCommitInterface.RecoverProvisionallyCommittedSaveData(in savesToRecover[i], true);

                    if (recoveryResult.IsSuccess() && rc.IsFailure())
                    {
                        recoveryResult = rc;
                    }
                }
            }

            var contextFilePath = new Fs.Path();
            rc = PathFunctions.SetUpFixedPath(ref contextFilePath, CommitContextFileName);
            if (rc.IsFailure()) return rc;

            // Delete the commit context file
            rc = contextFs.DeleteFile(in contextFilePath);
            if (rc.IsFailure()) return rc;

            rc = contextFs.Commit();
            if (rc.IsFailure()) return rc;

            contextFilePath.Dispose();
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
            ref MultiCommitManagerGlobals globals = ref fsServer.Globals.MultiCommitManager;
            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref globals.MultiCommitMutex);

            bool needsRecovery = true;
            ReferenceCountedDisposable<IFileSystem> fileSystem = null;

            try
            {
                // Check if a multi-commit was interrupted by checking if there's a commit context file.
                Result rc = multiCommitInterface.OpenMultiCommitContext(out fileSystem);

                if (rc.IsFailure())
                {
                    if (!ResultFs.PathNotFound.Includes(rc) && !ResultFs.TargetNotFound.Includes(rc))
                        return rc;

                    // Unable to open the multi-commit context file system, so there's nothing to recover
                    needsRecovery = false;
                }

                if (needsRecovery)
                {
                    var contextFilePath = new Fs.Path();
                    rc = PathFunctions.SetUpFixedPath(ref contextFilePath, CommitContextFileName);
                    if (rc.IsFailure()) return rc;

                    rc = fileSystem.Target.OpenFile(out IFile file, in contextFilePath, OpenMode.Read);
                    file?.Dispose();

                    if (rc.IsFailure())
                    {
                        // Unable to open the context file. No multi-commit to recover.
                        if (ResultFs.PathNotFound.Includes(rc))
                            needsRecovery = false;
                    }

                    contextFilePath.Dispose();
                }

                if (!needsRecovery)
                    return Result.Success;

                // There was a context file. Recover the unfinished commit.
                return Recover(multiCommitInterface, fileSystem.Target, saveService);
            }
            finally
            {
                fileSystem?.Dispose();
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 0x18)]
        private struct Context
        {
            [FieldOffset(0x00)] public int Version;
            [FieldOffset(0x04)] public CommitState State;
            [FieldOffset(0x08)] public int FileSystemCount;
            [FieldOffset(0x10)] public long Counter;
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

                var contextFilePath = new Fs.Path();
                PathFunctions.SetUpFixedPath(ref contextFilePath, CommitContextFileName).IgnoreResult();
                _fileSystem.DeleteFile(in contextFilePath).IgnoreResult();
                _fileSystem.Commit().IgnoreResult();

                _fileSystem = null;
                contextFilePath.Dispose();
            }

            /// <summary>
            /// Creates and writes the initial commit context to a file.
            /// </summary>
            /// <param name="counter">The counter.</param>
            /// <param name="fileSystemCount">The number of file systems being committed.</param>
            /// <returns>The <see cref="Result"/> of the operation.</returns>
            public Result Create(long counter, int fileSystemCount)
            {
                var contextFilePath = new Fs.Path();
                Result rc = PathFunctions.SetUpFixedPath(ref contextFilePath, CommitContextFileName);
                if (rc.IsFailure()) return rc;

                IFile contextFile = null;

                try
                {
                    // Open context file and create if it doesn't exist
                    rc = _fileSystem.OpenFile(out contextFile, in contextFilePath, OpenMode.Read);

                    if (rc.IsFailure())
                    {
                        if (!ResultFs.PathNotFound.Includes(rc))
                            return rc;

                        rc = _fileSystem.CreateFile(in contextFilePath, CommitContextFileSize);
                        if (rc.IsFailure()) return rc;

                        rc = _fileSystem.OpenFile(out contextFile, in contextFilePath, OpenMode.Read);
                        if (rc.IsFailure()) return rc;
                    }
                }
                finally
                {
                    contextFile?.Dispose();
                }

                try
                {
                    rc = _fileSystem.OpenFile(out contextFile, in contextFilePath, OpenMode.ReadWrite);
                    if (rc.IsFailure()) return rc;

                    _context.Version = CurrentCommitContextVersion;
                    _context.State = CommitState.NotCommitted;
                    _context.FileSystemCount = fileSystemCount;
                    _context.Counter = counter;

                    // Write the initial context to the file
                    rc = contextFile.Write(0, SpanHelpers.AsByteSpan(ref _context), WriteOption.None);
                    if (rc.IsFailure()) return rc;

                    rc = contextFile.Flush();
                    if (rc.IsFailure()) return rc;
                }
                finally
                {
                    contextFile?.Dispose();
                }

                rc = _fileSystem.Commit();
                if (rc.IsFailure()) return rc;

                contextFilePath.Dispose();
                return Result.Success;
            }

            /// <summary>
            /// Updates the commit context and writes it to a file, signifying that all
            /// the file systems have been provisionally committed.
            /// </summary>
            /// <returns>The <see cref="Result"/> of the operation.</returns>
            public Result CommitProvisionallyDone()
            {
                var contextFilePath = new Fs.Path();
                Result rc = PathFunctions.SetUpFixedPath(ref contextFilePath, CommitContextFileName);
                if (rc.IsFailure()) return rc;

                IFile contextFile = null;

                try
                {
                    rc = _fileSystem.OpenFile(out contextFile, in contextFilePath, OpenMode.ReadWrite);
                    if (rc.IsFailure()) return rc;

                    _context.State = CommitState.ProvisionallyCommitted;

                    rc = contextFile.Write(0, SpanHelpers.AsByteSpan(ref _context), WriteOption.None);
                    if (rc.IsFailure()) return rc;

                    rc = contextFile.Flush();
                    if (rc.IsFailure()) return rc;
                }
                finally
                {
                    contextFile?.Dispose();
                }

                contextFilePath.Dispose();
                return _fileSystem.Commit();
            }

            /// <summary>
            /// To be called once the multi-commit has been successfully completed. Deletes the commit context file.
            /// </summary>
            /// <returns>The <see cref="Result"/> of the operation.</returns>
            public Result CommitDone()
            {
                var contextFilePath = new Fs.Path();
                Result rc = PathFunctions.SetUpFixedPath(ref contextFilePath, CommitContextFileName);
                if (rc.IsFailure()) return rc;

                rc = _fileSystem.DeleteFile(in contextFilePath);
                if (rc.IsFailure()) return rc;

                rc = _fileSystem.Commit();
                if (rc.IsFailure()) return rc;

                _fileSystem = null;
                contextFilePath.Dispose();
                return Result.Success;
            }
        }
    }
}
