using System.Collections.Generic;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Shim;

namespace LibHac.FsService.Impl
{
    internal class MultiCommitManager : IMultiCommitManager
    {
        private const int MaxFileSystemCount = 10;
        private const int CurrentContextVersion = 0x10000;

        public const ulong ProgramId = 0x100000000000000;
        public const ulong SaveDataId = 0x8000000000000001;
        private const long SaveDataSize = 0xC000;
        private const long SaveJournalSize = 0xC000;

        private const long ContextFileSize = 0x200;

        // /commitinfo
        private static U8Span ContextFileName =>
            new U8Span(new[] { (byte)'/', (byte)'c', (byte)'o', (byte)'m', (byte)'m', (byte)'i', (byte)'t', (byte)'i', (byte)'n', (byte)'f', (byte)'o' });

        private static readonly object Locker = new object();

        private FileSystemProxy FsProxy { get; }
        private List<IFileSystem> FileSystems { get; } = new List<IFileSystem>(MaxFileSystemCount);
        private long CommitCount { get; set; }

        public MultiCommitManager(FileSystemProxy fsProxy)
        {
            FsProxy = fsProxy;
        }

        public Result Add(IFileSystem fileSystem)
        {
            if (FileSystems.Count >= MaxFileSystemCount)
                return ResultFs.MultiCommitFileSystemLimit.Log();

            // Check that the file system hasn't already been added
            for (int i = 0; i < FileSystems.Count; i++)
            {
                if (ReferenceEquals(FileSystems[i], fileSystem))
                    return ResultFs.MultiCommitFileSystemAlreadyAdded.Log();
            }

            FileSystems.Add(fileSystem);
            return Result.Success;
        }

        public Result Commit()
        {
            lock (Locker)
            {
                Result rc = CreateSave();
                if (rc.IsFailure()) return rc;

                rc = FsProxy.OpenMultiCommitContextSaveData(out IFileSystem contextFs);
                if (rc.IsFailure()) return rc;

                return CommitImpl(contextFs);
            }
        }

        private Result CommitImpl(IFileSystem contextFileSystem)
        {
            var context = new CommitContextManager(contextFileSystem);

            try
            {
                CommitCount = 1;

                Result rc = context.Initialize(CommitCount, FileSystems.Count);
                if (rc.IsFailure()) return rc;

                rc = CommitProvisionally();
                if (rc.IsFailure()) return rc;

                rc = context.SetCommittedProvisionally();
                if (rc.IsFailure()) return rc;

                foreach (IFileSystem fs in FileSystems)
                {
                    rc = fs.Commit();
                    if (rc.IsFailure()) return rc;
                }

                rc = context.Close();
                if (rc.IsFailure()) return rc;
            }
            finally
            {
                context.Dispose();
            }

            return Result.Success;
        }

        private Result CreateSave()
        {
            Result rc = FsProxy.OpenMultiCommitContextSaveData(out IFileSystem contextFs);

            if (rc.IsFailure())
            {
                if (!ResultFs.TargetNotFound.Includes(rc))
                {
                    return rc;
                }

                rc = FsProxy.FsServer.FsClient.CreateSystemSaveData(SaveDataId, SaveDataSize, SaveJournalSize,
                    SaveDataFlags.None);
                if (rc.IsFailure()) return rc;
            }

            contextFs?.Dispose();
            return Result.Success;
        }

        private Result CommitProvisionally()
        {
            Result rc = Result.Success;
            int i;

            for (i = 0; i < FileSystems.Count; i++)
            {
                rc = FileSystems[i].CommitProvisionally(CommitCount);

                if (rc.IsFailure())
                    break;
            }

            if (rc.IsFailure())
            {
                // Rollback all provisional commits including the failed commit
                for (int j = 0; j <= i; j++)
                {
                    FileSystems[j].Rollback().IgnoreResult();
                }
            }

            return rc;
        }

        [StructLayout(LayoutKind.Explicit, Size = 0x18)]
        private struct CommitContext
        {
            [FieldOffset(0x00)] public int Version;
            [FieldOffset(0x04)] public CommitState State;
            [FieldOffset(0x08)] public int FileSystemCount;
            [FieldOffset(0x10)] public long CommitCount; // I think?
        }

        private enum CommitState
        {
            // ReSharper disable once UnusedMember.Local
            None = 0,
            NotCommitted = 1,
            ProvisionallyCommitted = 2
        }

        private struct CommitContextManager
        {
            private IFileSystem _fileSystem;
            private CommitContext _context;

            public CommitContextManager(IFileSystem contextFileSystem)
            {
                _fileSystem = contextFileSystem;
                _context = default;
            }

            public Result Initialize(long commitCount, int fileSystemCount)
            {
                IFile contextFile = null;

                try
                {
                    // Open context file and create if it doesn't exist
                    Result rc = _fileSystem.OpenFile(out contextFile, ContextFileName, OpenMode.Read);

                    if (rc.IsFailure())
                    {
                        if (!ResultFs.PathNotFound.Includes(rc))
                            return rc;

                        rc = _fileSystem.CreateFile(ContextFileName, ContextFileSize, CreateFileOptions.None);
                        if (rc.IsFailure()) return rc;

                        rc = _fileSystem.OpenFile(out contextFile, ContextFileName, OpenMode.Read);
                        if (rc.IsFailure()) return rc;
                    }
                }
                finally
                {
                    contextFile?.Dispose();
                }

                try
                {
                    Result rc = _fileSystem.OpenFile(out contextFile, ContextFileName, OpenMode.ReadWrite);
                    if (rc.IsFailure()) return rc;

                    _context.Version = CurrentContextVersion;
                    _context.State = CommitState.NotCommitted;
                    _context.FileSystemCount = fileSystemCount;
                    _context.CommitCount = commitCount;

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

            public Result SetCommittedProvisionally()
            {
                IFile contextFile = null;

                try
                {
                    Result rc = _fileSystem.OpenFile(out contextFile, ContextFileName, OpenMode.ReadWrite);
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

            public Result Close()
            {
                Result rc = _fileSystem.DeleteFile(ContextFileName);
                if (rc.IsFailure()) return rc;

                rc = _fileSystem.Commit();
                if (rc.IsFailure()) return rc;

                _fileSystem = null;
                return Result.Success;
            }

            public void Dispose()
            {
                if (_fileSystem is null) return;

                _fileSystem.DeleteFile(ContextFileName).IgnoreResult();
                _fileSystem.Commit().IgnoreResult();

                _fileSystem = null;
            }
        }
    }
}
