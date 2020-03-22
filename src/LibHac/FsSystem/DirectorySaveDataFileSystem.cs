using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class DirectorySaveDataFileSystem : FileSystemBase
    {
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

        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private bool IsUserSaveData { get; set; }

        public static Result CreateNew(out DirectorySaveDataFileSystem created, IFileSystem baseFileSystem,
            bool isPersistentSaveData, bool isUserSaveData)
        {
            var obj = new DirectorySaveDataFileSystem(baseFileSystem);
            Result rc = obj.Initialize(isPersistentSaveData, isUserSaveData);

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

        private Result Initialize(bool isPersistentSaveData, bool isUserSaveData)
        {
            IsPersistentSaveData = isPersistentSaveData;
            IsUserSaveData = isUserSaveData;

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

        protected override Result CreateDirectoryImpl(U8Span path)
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

        protected override Result CreateFileImpl(U8Span path, long size, CreateFileOptions options)
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

        protected override Result DeleteDirectoryImpl(U8Span path)
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

        protected override Result DeleteDirectoryRecursivelyImpl(U8Span path)
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

        protected override Result CleanDirectoryRecursivelyImpl(U8Span path)
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

        protected override Result DeleteFileImpl(U8Span path)
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

        protected override Result OpenDirectoryImpl(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
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

        protected override Result OpenFileImpl(out IFile file, U8Span path, OpenMode mode)
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

        protected override Result RenameDirectoryImpl(U8Span oldPath, U8Span newPath)
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

        protected override Result RenameFileImpl(U8Span oldPath, U8Span newPath)
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

        protected override Result GetEntryTypeImpl(out DirectoryEntryType entryType, U8Span path)
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

        protected override Result CommitImpl()
        {
            lock (Locker)
            {
                if (!IsPersistentSaveData) return Result.Success;

                if (OpenWritableFileCount > 0)
                {
                    // All files must be closed before commiting save data.
                    return ResultFs.WriteModeFileNotClosed.Log();
                }

                // Get rid of the previous commit by renaming the folder
                Result rc = BaseFs.RenameDirectory(CommittedDirectoryPath, SynchronizingDirectoryPath);
                if (rc.IsFailure()) return rc;

                // If something goes wrong beyond this point, the commit will be
                // completed the next time the savedata is opened

                rc = SynchronizeDirectory(SynchronizingDirectoryPath, WorkingDirectoryPath);
                if (rc.IsFailure()) return rc;

                return BaseFs.RenameDirectory(SynchronizingDirectoryPath, CommittedDirectoryPath);
            }
        }

        protected override Result CommitProvisionallyImpl(long commitCount)
        {
            if (!IsUserSaveData)
                return ResultFs.UnsupportedOperationIdInPartitionFileSystem.Log();

            return Result.Success;
        }

        protected override Result RollbackImpl()
        {
            // No old data is kept for temporary save data, so there's nothing to rollback to
            if (!IsPersistentSaveData)
                return Result.Success;

            return Initialize(IsPersistentSaveData, IsUserSaveData);
        }

        private Result ResolveFullPath(Span<byte> outPath, U8Span relativePath)
        {
            if (StringUtils.GetLength(relativePath, PathTools.MaxPathLength + 1) > PathTools.MaxPathLength)
                return ResultFs.TooLongPath.Log();

            StringUtils.Copy(outPath, WorkingDirectoryBytes);
            outPath[^1] = StringTraits.NullTerminator;

            return PathTool.Normalize(outPath.Slice(2), out _, relativePath, false, false);
        }

        private Result SynchronizeDirectory(U8Span dest, U8Span src)
        {
            Result rc = BaseFs.DeleteDirectoryRecursively(dest);
            if (rc.IsFailure() && !ResultFs.PathNotFound.Includes(rc)) return rc;

            rc = BaseFs.CreateDirectory(dest);
            if (rc.IsFailure()) return rc;

            return BaseFs.CopyDirectory(BaseFs, src.ToString(), dest.ToString());
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
