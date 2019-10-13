using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class DirectorySaveDataFileSystem : FileSystemBase
    {
        private const string CommittedDir = "/0/";
        private const string WorkingDir = "/1/";
        private const string SyncDir = "/_/";

        private IFileSystem BaseFs { get; }
        private object Locker { get; } = new object();
        private int OpenWritableFileCount { get; set; }
        private bool IsPersistentSaveData { get; set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private bool IsUserSaveData { get; set; }

        public static Result CreateNew(out DirectorySaveDataFileSystem created, IFileSystem baseFileSystem)
        {
            return CreateNew(out created, baseFileSystem, true, true);
        }

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
            Result rc = BaseFs.GetEntryType(out _, WorkingDir);

            if (rc.IsFailure())
            {
                if (rc != ResultFs.PathNotFound) return rc;

                rc = BaseFs.CreateDirectory(WorkingDir);
                if (rc.IsFailure()) return rc;

                if (!IsPersistentSaveData) return Result.Success;

                rc = BaseFs.CreateDirectory(CommittedDir);

                // Nintendo returns on all failures, but we'll keep going if committed already exists
                // to avoid confusing people manually creating savedata in emulators
                if (rc.IsFailure() && rc != ResultFs.PathAlreadyExists) return rc;
            }

            // Only the working directory is needed for temporary savedata
            if (!IsPersistentSaveData) return Result.Success;

            rc = BaseFs.GetEntryType(out _, CommittedDir);

            if (rc.IsSuccess())
            {
                return SynchronizeDirectory(WorkingDir, CommittedDir);
            }
            
            if (rc != ResultFs.PathNotFound) return rc;

            // If a previous commit failed, the committed dir may be missing.
            // Finish that commit by copying the working dir to the committed dir

            rc = SynchronizeDirectory(SyncDir, WorkingDir);
            if (rc.IsFailure()) return rc;

            return BaseFs.RenameDirectory(SyncDir, CommittedDir);
        }

        protected override Result CreateDirectoryImpl(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.CreateDirectory(fullPath);
            }
        }

        protected override Result CreateFileImpl(string path, long size, CreateFileOptions options)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.CreateFile(fullPath, size, options);
            }
        }

        protected override Result DeleteDirectoryImpl(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.DeleteDirectory(fullPath);
            }
        }

        protected override Result DeleteDirectoryRecursivelyImpl(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.DeleteDirectoryRecursively(fullPath);
            }
        }

        protected override Result CleanDirectoryRecursivelyImpl(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.CleanDirectoryRecursively(fullPath);
            }
        }

        protected override Result DeleteFileImpl(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.DeleteFile(fullPath);
            }
        }

        protected override Result OpenDirectoryImpl(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.OpenDirectory(out directory, fullPath, mode);
            }
        }

        protected override Result OpenFileImpl(out IFile file, string path, OpenMode mode)
        {
            file = default;
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                Result rc = BaseFs.OpenFile(out IFile baseFile, fullPath, mode);
                if (rc.IsFailure()) return rc;

                file = new DirectorySaveDataFile(this, baseFile, mode);

                if (mode.HasFlag(OpenMode.Write))
                {
                    OpenWritableFileCount++;
                }

                return Result.Success;
            }
        }

        protected override Result RenameDirectoryImpl(string oldPath, string newPath)
        {
            string fullOldPath = GetFullPath(PathTools.Normalize(oldPath));
            string fullNewPath = GetFullPath(PathTools.Normalize(newPath));

            lock (Locker)
            {
                return BaseFs.RenameDirectory(fullOldPath, fullNewPath);
            }
        }

        protected override Result RenameFileImpl(string oldPath, string newPath)
        {
            string fullOldPath = GetFullPath(PathTools.Normalize(oldPath));
            string fullNewPath = GetFullPath(PathTools.Normalize(newPath));

            lock (Locker)
            {
                return BaseFs.RenameFile(fullOldPath, fullNewPath);
            }
        }

        protected override Result GetEntryTypeImpl(out DirectoryEntryType entryType, string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.GetEntryType(out entryType, fullPath);
            }
        }

        protected override Result CommitImpl()
        {
            lock (Locker)
            {
                if (OpenWritableFileCount > 0)
                {
                    // All files must be closed before commiting save data.
                    return ResultFs.WritableFileOpen.Log();
                }

                // Get rid of the previous commit by renaming the folder
                Result rc = BaseFs.RenameDirectory(CommittedDir, SyncDir);
                if (rc.IsFailure()) return rc;

                // If something goes wrong beyond this point, the commit will be
                // completed the next time the savedata is opened

                rc = SynchronizeDirectory(SyncDir, WorkingDir);
                if (rc.IsFailure()) return rc;

                return BaseFs.RenameDirectory(SyncDir, CommittedDir);
            }
        }

        private string GetFullPath(string path)
        {
            return PathTools.Normalize(PathTools.Combine(WorkingDir, path));
        }

        private Result SynchronizeDirectory(string dest, string src)
        {
            Result rc = BaseFs.DeleteDirectoryRecursively(dest);
            if (rc.IsFailure() && rc != ResultFs.PathNotFound) return rc;

            rc = BaseFs.CreateDirectory(dest);
            if (rc.IsFailure()) return rc;

            return BaseFs.CopyDirectory(BaseFs, src, dest);
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
