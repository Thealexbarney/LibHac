using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Sf;
using LibHac.Sf;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFile = LibHac.Fs.Fsa.IFile;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.FsSrv.Impl
{
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
        private static U8Span CommitContextFileName =>
            new U8Span(new[] { (byte)'/', (byte)'c', (byte)'o', (byte)'m', (byte)'m', (byte)'i', (byte)'t', (byte)'i', (byte)'n', (byte)'f', (byte)'o' });

        // Todo: Don't use global lock object
        private static readonly object Locker = new object();

        private ReferenceCountedDisposable<ISaveDataMultiCommitCoreInterface> MultiCommitInterface { get; }

        private List<ReferenceCountedDisposable<IFileSystem>> FileSystems { get; } =
            new List<ReferenceCountedDisposable<IFileSystem>>(MaxFileSystemCount);

        private long Counter { get; set; }
        private HorizonClient Hos { get; }

        public MultiCommitManager(
            ref ReferenceCountedDisposable<ISaveDataMultiCommitCoreInterface> multiCommitInterface,
            HorizonClient client)
        {
            Hos = client;
            MultiCommitInterface = Shared.Move(ref multiCommitInterface);
        }

        public static ReferenceCountedDisposable<IMultiCommitManager> CreateShared(
            ref ReferenceCountedDisposable<ISaveDataMultiCommitCoreInterface> multiCommitInterface,
            HorizonClient client)
        {
            var manager = new MultiCommitManager(ref multiCommitInterface, client);
            return new ReferenceCountedDisposable<IMultiCommitManager>(manager);
        }

        public void Dispose()
        {
            foreach (ReferenceCountedDisposable<IFileSystem> fs in FileSystems)
            {
                fs.Dispose();
            }
        }

        /// <summary>
        /// Ensures the save data used to store the commit context exists.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        private Result EnsureSaveDataForContext()
        {
            Result rc = MultiCommitInterface.Target.OpenMultiCommitContext(
                out ReferenceCountedDisposable<IFileSystem> contextFs);

            if (rc.IsFailure())
            {
                if (!ResultFs.TargetNotFound.Includes(rc))
                    return rc;

                rc = Hos.Fs.CreateSystemSaveData(SaveDataId, SaveDataSize, SaveJournalSize, SaveDataFlags.None);
                if (rc.IsFailure()) return rc;
            }

            contextFs?.Dispose();
            return Result.Success;
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
            if (FileSystems.Count >= MaxFileSystemCount)
                return ResultFs.MultiCommitFileSystemLimit.Log();

            ReferenceCountedDisposable<IFileSystem> fsaFileSystem = null;
            try
            {
                Result rc = fileSystem.Target.GetImpl(out fsaFileSystem);
                if (rc.IsFailure()) return rc;

                // Check that the file system hasn't already been added
                foreach (ReferenceCountedDisposable<IFileSystem> fs in FileSystems)
                {
                    if (ReferenceEquals(fs.Target, fsaFileSystem.Target))
                        return ResultFs.MultiCommitFileSystemAlreadyAdded.Log();
                }

                FileSystems.Add(fsaFileSystem);
                fsaFileSystem = null;

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
            ContextUpdater context = default;

            try
            {
                Counter = 1;

                context = new ContextUpdater(contextFileSystem);
                Result rc = context.Create(Counter, FileSystems.Count);
                if (rc.IsFailure()) return rc;

                rc = CommitProvisionallyFileSystem(Counter);
                if (rc.IsFailure()) return rc;

                rc = context.CommitProvisionallyDone();
                if (rc.IsFailure()) return rc;

                rc = CommitFileSystem();
                if (rc.IsFailure()) return rc;

                rc = context.CommitDone();
                if (rc.IsFailure()) return rc;
            }
            finally
            {
                context.Dispose();
            }

            return Result.Success;
        }

        /// <summary>
        /// Commits all added file systems.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result Commit()
        {
            lock (Locker)
            {
                Result rc = EnsureSaveDataForContext();
                if (rc.IsFailure()) return rc;

                ReferenceCountedDisposable<IFileSystem> contextFs = null;
                try
                {
                    rc = MultiCommitInterface.Target.OpenMultiCommitContext(out contextFs);
                    if (rc.IsFailure()) return rc;

                    return Commit(contextFs.Target);
                }
                finally
                {
                    contextFs?.Dispose();
                }
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

            for (i = 0; i < FileSystems.Count; i++)
            {
                rc = FileSystems[i].Target.CommitProvisionally(counter);

                if (rc.IsFailure())
                    break;
            }

            if (rc.IsFailure())
            {
                // Rollback all provisional commits including the failed commit
                for (int j = 0; j <= i; j++)
                {
                    FileSystems[j].Target.Rollback().IgnoreResult();
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
            // All file systems will try to be recovered committed, even if one fails.
            // If any commits fail, the result from the first failed recovery will be returned.
            Result result = Result.Success;

            foreach (ReferenceCountedDisposable<IFileSystem> fs in FileSystems)
            {
                Result rc = fs.Target.Commit();

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
            IFile contextFile = null;
            try
            {
                // Read the multi-commit context
                Result rc = contextFs.OpenFile(out contextFile, CommitContextFileName, OpenMode.ReadWrite);
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

                    if (recoveryResult.IsSuccess() && rc.IsFailure())
                    {
                        recoveryResult = rc;
                    }
                }

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

            // Delete the commit context file
            rc = contextFs.DeleteFile(CommitContextFileName);
            if (rc.IsFailure()) return rc;

            rc = contextFs.Commit();
            if (rc.IsFailure()) return rc;

            return recoveryResult;
        }

        /// <summary>
        /// Recovers an interrupted multi-commit. The commit will either be completed or rolled back depending on
        /// where in the commit process it was interrupted. Does nothing if there is no commit to recover.
        /// </summary>
        /// <param name="multiCommitInterface">The core interface used for multi-commits.</param>
        /// <param name="saveService">The save data service.</param>
        /// <returns>The <see cref="Result"/> of the operation.<br/>
        /// <see cref="Result.Success"/>: The recovery was successful or there was no multi-commit to recover.</returns>
        public static Result Recover(ISaveDataMultiCommitCoreInterface multiCommitInterface,
            SaveDataFileSystemServiceImpl saveService)
        {
            lock (Locker)
            {
                bool needsRecover = true;
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
                        needsRecover = false;
                    }

                    if (needsRecover)
                    {
                        rc = fileSystem.Target.OpenFile(out IFile file, CommitContextFileName, OpenMode.Read);
                        file?.Dispose();

                        if (rc.IsFailure())
                        {
                            // Unable to open the context file. No multi-commit to recover.
                            if (ResultFs.PathNotFound.Includes(rc))
                                needsRecover = false;
                        }
                    }

                    if (!needsRecover)
                        return Result.Success;

                    // There was a context file. Recover the unfinished commit.
                    return Recover(multiCommitInterface, fileSystem.Target, saveService);
                }
                finally
                {
                    fileSystem?.Dispose();
                }
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

        private struct ContextUpdater
        {
            private IFileSystem _fileSystem;
            private Context _context;

            public ContextUpdater(IFileSystem contextFileSystem)
            {
                _fileSystem = contextFileSystem;
                _context = default;
            }

            /// <summary>
            /// Creates and writes the initial commit context to a file.
            /// </summary>
            /// <param name="commitCount">The counter.</param>
            /// <param name="fileSystemCount">The number of file systems being committed.</param>
            /// <returns>The <see cref="Result"/> of the operation.</returns>
            public Result Create(long commitCount, int fileSystemCount)
            {
                IFile contextFile = null;

                try
                {
                    // Open context file and create if it doesn't exist
                    Result rc = _fileSystem.OpenFile(out contextFile, CommitContextFileName, OpenMode.Read);

                    if (rc.IsFailure())
                    {
                        if (!ResultFs.PathNotFound.Includes(rc))
                            return rc;

                        rc = _fileSystem.CreateFile(CommitContextFileName, CommitContextFileSize, CreateFileOptions.None);
                        if (rc.IsFailure()) return rc;

                        rc = _fileSystem.OpenFile(out contextFile, CommitContextFileName, OpenMode.Read);
                        if (rc.IsFailure()) return rc;
                    }
                }
                finally
                {
                    contextFile?.Dispose();
                }

                try
                {
                    Result rc = _fileSystem.OpenFile(out contextFile, CommitContextFileName, OpenMode.ReadWrite);
                    if (rc.IsFailure()) return rc;

                    _context.Version = CurrentCommitContextVersion;
                    _context.State = CommitState.NotCommitted;
                    _context.FileSystemCount = fileSystemCount;
                    _context.Counter = commitCount;

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

                return _fileSystem.Commit();
            }

            /// <summary>
            /// Updates the commit context and writes it to a file, signifying that all
            /// the file systems have been provisionally committed.
            /// </summary>
            /// <returns>The <see cref="Result"/> of the operation.</returns>
            public Result CommitProvisionallyDone()
            {
                IFile contextFile = null;

                try
                {
                    Result rc = _fileSystem.OpenFile(out contextFile, CommitContextFileName, OpenMode.ReadWrite);
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

                return _fileSystem.Commit();
            }

            /// <summary>
            /// To be called once the multi-commit has been successfully completed. Deletes the commit context file.
            /// </summary>
            /// <returns>The <see cref="Result"/> of the operation.</returns>
            public Result CommitDone()
            {
                Result rc = _fileSystem.DeleteFile(CommitContextFileName);
                if (rc.IsFailure()) return rc;

                rc = _fileSystem.Commit();
                if (rc.IsFailure()) return rc;

                _fileSystem = null;
                return Result.Success;
            }

            public void Dispose()
            {
                if (_fileSystem is null) return;

                _fileSystem.DeleteFile(CommitContextFileName).IgnoreResult();
                _fileSystem.Commit().IgnoreResult();

                _fileSystem = null;
            }
        }
    }
}
