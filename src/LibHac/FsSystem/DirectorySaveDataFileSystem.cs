using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    /// <summary>
    /// An <see cref="IFileSystem"/> that provides transactional commits for savedata on top of another base IFileSystem.
    /// </summary>
    /// <remarks>
    /// Transactional commits should be atomic as long as the <see cref="IFileSystem.RenameDirectory"/> function of the
    /// underlying <see cref="IFileSystem"/> is atomic.
    /// This class is based on nn::fssystem::DirectorySaveDataFileSystem in SDK 10.4.0 used in FS 10.0.0
    /// </remarks>
    public class DirectorySaveDataFileSystem : IFileSystem
    {
        private const int IdealWorkBufferSize = 0x100000; // 1 MiB

        private ReadOnlySpan<byte> CommittedDirectoryBytes => new[] { (byte)'/', (byte)'0', (byte)'/' };
        private ReadOnlySpan<byte> WorkingDirectoryBytes => new[] { (byte)'/', (byte)'1', (byte)'/' };
        private ReadOnlySpan<byte> SynchronizingDirectoryBytes => new[] { (byte)'/', (byte)'_', (byte)'/' };

        private U8Span CommittedDirectoryPath => new U8Span(CommittedDirectoryBytes);
        private U8Span WorkingDirectoryPath => new U8Span(WorkingDirectoryBytes);
        private U8Span SynchronizingDirectoryPath => new U8Span(SynchronizingDirectoryBytes);

        private IFileSystem BaseFs { get; }
        private object Locker { get; } = new object();
        private int OpenWritableFileCount { get; set; }
        private bool IsPersistentSaveData { get; set; }
        private bool CanCommitProvisionally { get; set; }

        public static Result CreateNew(out DirectorySaveDataFileSystem created, IFileSystem baseFileSystem,
            bool isPersistentSaveData, bool canCommitProvisionally)
        {
            var obj = new DirectorySaveDataFileSystem(baseFileSystem);
            Result rc = obj.Initialize(isPersistentSaveData, canCommitProvisionally);

            if (rc.IsSuccess())
            {
                created = obj;
                return Result.Success;
            }

            obj.Dispose();
            created = default;
            return rc;
        }

        private DirectorySaveDataFileSystem(IFileSystem baseFileSystem)
        {
            BaseFs = baseFileSystem;
        }

        private Result Initialize(bool isPersistentSaveData, bool canCommitProvisionally)
        {
            IsPersistentSaveData = isPersistentSaveData;
            CanCommitProvisionally = canCommitProvisionally;

            // Ensure the working directory exists
            Result rc = BaseFs.GetEntryType(out _, WorkingDirectoryPath);

            if (rc.IsFailure())
            {
                if (!ResultFs.PathNotFound.Includes(rc)) return rc;

                rc = BaseFs.CreateDirectory(WorkingDirectoryPath);
                if (rc.IsFailure()) return rc;

                if (!IsPersistentSaveData) return Result.Success;

                rc = BaseFs.CreateDirectory(CommittedDirectoryPath);

                // Nintendo returns on all failures, but we'll keep going if committed already exists
                // to avoid confusing people manually creating savedata in emulators
                if (rc.IsFailure() && !ResultFs.PathAlreadyExists.Includes(rc)) return rc;
            }

            // Only the working directory is needed for temporary savedata
            if (!IsPersistentSaveData) return Result.Success;

            rc = BaseFs.GetEntryType(out _, CommittedDirectoryPath);

            if (rc.IsSuccess())
            {
                return SynchronizeDirectory(WorkingDirectoryPath, CommittedDirectoryPath);
            }

            if (!ResultFs.PathNotFound.Includes(rc)) return rc;

            // If a previous commit failed, the committed dir may be missing.
            // Finish that commit by copying the working dir to the committed dir

            rc = SynchronizeDirectory(SynchronizingDirectoryPath, WorkingDirectoryPath);
            if (rc.IsFailure()) return rc;

            return BaseFs.RenameDirectory(SynchronizingDirectoryPath, CommittedDirectoryPath);
        }

        protected override Result DoCreateDirectory(U8Span path)
        {
            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            lock (Locker)
            {
                return BaseFs.CreateDirectory(fullPath);
            }
        }

        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions options)
        {
            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            lock (Locker)
            {
                return BaseFs.CreateFile(fullPath, size, options);
            }
        }

        protected override Result DoDeleteDirectory(U8Span path)
        {
            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            lock (Locker)
            {
                return BaseFs.DeleteDirectory(fullPath);
            }
        }

        protected override Result DoDeleteDirectoryRecursively(U8Span path)
        {
            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            lock (Locker)
            {
                return BaseFs.DeleteDirectoryRecursively(fullPath);
            }
        }

        protected override Result DoCleanDirectoryRecursively(U8Span path)
        {
            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            lock (Locker)
            {
                return BaseFs.CleanDirectoryRecursively(fullPath);
            }
        }

        protected override Result DoDeleteFile(U8Span path)
        {
            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            lock (Locker)
            {
                return BaseFs.DeleteFile(fullPath);
            }
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure())
            {
                directory = default;
                return rc;
            }

            lock (Locker)
            {
                return BaseFs.OpenDirectory(out directory, fullPath, mode);
            }
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            file = default;

            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            lock (Locker)
            {
                rc = BaseFs.OpenFile(out IFile baseFile, fullPath, mode);
                if (rc.IsFailure()) return rc;

                file = new DirectorySaveDataFile(this, baseFile, mode);

                if (mode.HasFlag(OpenMode.Write))
                {
                    OpenWritableFileCount++;
                }

                return Result.Success;
            }
        }

        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath)
        {
            FsPath fullCurrentPath;
            FsPath fullNewPath;
            unsafe { _ = &fullCurrentPath; } // workaround for CS0165
            unsafe { _ = &fullNewPath; } // workaround for CS0165

            Result rc = ResolveFullPath(fullCurrentPath.Str, oldPath);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(fullNewPath.Str, newPath);
            if (rc.IsFailure()) return rc;

            lock (Locker)
            {
                return BaseFs.RenameDirectory(fullCurrentPath, fullNewPath);
            }
        }

        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath)
        {
            FsPath fullCurrentPath;
            FsPath fullNewPath;
            unsafe { _ = &fullCurrentPath; } // workaround for CS0165
            unsafe { _ = &fullNewPath; } // workaround for CS0165

            Result rc = ResolveFullPath(fullCurrentPath.Str, oldPath);
            if (rc.IsFailure()) return rc;

            rc = ResolveFullPath(fullNewPath.Str, newPath);
            if (rc.IsFailure()) return rc;

            lock (Locker)
            {
                return BaseFs.RenameFile(fullCurrentPath, fullNewPath);
            }
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure())
            {
                entryType = default;
                return rc;
            }

            lock (Locker)
            {
                return BaseFs.GetEntryType(out entryType, fullPath);
            }
        }

        protected override Result DoCommit()
        {
            lock (Locker)
            {
                if (!IsPersistentSaveData)
                    return Result.Success;

                if (OpenWritableFileCount > 0)
                {
                    // All files must be closed before commiting save data.
                    return ResultFs.WriteModeFileNotClosed.Log();
                }

                Result RenameCommittedDir() => BaseFs.RenameDirectory(CommittedDirectoryPath, SynchronizingDirectoryPath);
                Result SynchronizeWorkingDir() => SynchronizeDirectory(SynchronizingDirectoryPath, WorkingDirectoryPath);
                Result RenameSynchronizingDir() => BaseFs.RenameDirectory(SynchronizingDirectoryPath, CommittedDirectoryPath);

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
        }

        protected override Result DoCommitProvisionally(long counter)
        {
            if (!CanCommitProvisionally)
                return ResultFs.UnsupportedOperationInDirectorySaveDataFileSystem.Log();

            return Result.Success;
        }

        protected override Result DoRollback()
        {
            // No old data is kept for temporary save data, so there's nothing to rollback to
            if (!IsPersistentSaveData)
                return Result.Success;

            return Initialize(IsPersistentSaveData, CanCommitProvisionally);
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            freeSpace = default;

            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            lock (Locker)
            {
                return BaseFs.GetFreeSpaceSize(out freeSpace, fullPath);
            }
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            totalSpace = default;

            FsPath fullPath;
            unsafe { _ = &fullPath; } // workaround for CS0165

            Result rc = ResolveFullPath(fullPath.Str, path);
            if (rc.IsFailure()) return rc;

            lock (Locker)
            {
                return BaseFs.GetTotalSpaceSize(out totalSpace, fullPath);
            }
        }

        private Result ResolveFullPath(Span<byte> outPath, U8Span relativePath)
        {
            if (StringUtils.GetLength(relativePath, PathTools.MaxPathLength + 1) > PathTools.MaxPathLength)
                return ResultFs.TooLongPath.Log();

            StringUtils.Copy(outPath, WorkingDirectoryBytes);
            outPath[outPath.Length - 1] = StringTraits.NullTerminator;

            return PathTool.Normalize(outPath.Slice(2), out _, relativePath, false, false);
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
            Result rc = BaseFs.DeleteDirectoryRecursively(destPath);

            // Nintendo returns error unconditionally because SynchronizeDirectory is always called in situations
            // where a PathNotFound error would mean the save directory was in an invalid state.
            // We'll ignore PathNotFound errors to be more user-friendly to users who might accidentally
            // put the save directory in an invalid state.
            if (rc.IsFailure() && !ResultFs.PathNotFound.Includes(rc)) return rc;

            rc = BaseFs.CreateDirectory(destPath);
            if (rc.IsFailure()) return rc;

            // Get a work buffer to work with.
            using (var buffer = new RentedArray<byte>(IdealWorkBufferSize))
            {
                return Utility.CopyDirectoryRecursively(BaseFs, destPath, sourcePath, buffer.Span);
            }
        }

        internal void NotifyCloseWritableFile()
        {
            lock (Locker)
            {
                OpenWritableFileCount--;
            }
        }
    }
}
