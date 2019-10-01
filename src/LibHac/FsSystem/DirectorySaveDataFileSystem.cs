using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class DirectorySaveDataFileSystem : IFileSystem
    {
        private const string CommittedDir = "/0/";
        private const string WorkingDir = "/1/";
        private const string SyncDir = "/_/";

        private IFileSystem BaseFs { get; }
        private object Locker { get; } = new object();
        private int OpenWritableFileCount { get; set; }

        public DirectorySaveDataFileSystem(IFileSystem baseFileSystem)
        {
            BaseFs = baseFileSystem;

            if (!BaseFs.DirectoryExists(WorkingDir))
            {
                BaseFs.CreateDirectory(WorkingDir);
                BaseFs.EnsureDirectoryExists(CommittedDir);
            }

            if (BaseFs.DirectoryExists(CommittedDir))
            {
                SynchronizeDirectory(WorkingDir, CommittedDir);
            }
            else
            {
                SynchronizeDirectory(SyncDir, WorkingDir);
                BaseFs.RenameDirectory(SyncDir, CommittedDir);
            }
        }

        public Result CreateDirectory(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.CreateDirectory(fullPath);
            }
        }

        public Result CreateFile(string path, long size, CreateFileOptions options)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.CreateFile(fullPath, size, options);
            }
        }

        public Result DeleteDirectory(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.DeleteDirectory(fullPath);
            }
        }

        public Result DeleteDirectoryRecursively(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.DeleteDirectoryRecursively(fullPath);
            }
        }

        public Result CleanDirectoryRecursively(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.CleanDirectoryRecursively(fullPath);
            }
        }

        public Result DeleteFile(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.DeleteFile(fullPath);
            }
        }

        public Result OpenDirectory(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.OpenDirectory(out directory, fullPath, mode);
            }
        }

        public Result OpenFile(out IFile file, string path, OpenMode mode)
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

        public Result RenameDirectory(string oldPath, string newPath)
        {
            string fullOldPath = GetFullPath(PathTools.Normalize(oldPath));
            string fullNewPath = GetFullPath(PathTools.Normalize(newPath));

            lock (Locker)
            {
                return BaseFs.RenameDirectory(fullOldPath, fullNewPath);
            }
        }

        public Result RenameFile(string oldPath, string newPath)
        {
            string fullOldPath = GetFullPath(PathTools.Normalize(oldPath));
            string fullNewPath = GetFullPath(PathTools.Normalize(newPath));

            lock (Locker)
            {
                return BaseFs.RenameFile(fullOldPath, fullNewPath);
            }
        }

        public Result GetEntryType(out DirectoryEntryType entryType, string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.GetEntryType(out entryType, fullPath);
            }
        }

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            freeSpace = default;
            return ResultFs.NotImplemented.Log();
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            totalSpace = default;
            return ResultFs.NotImplemented.Log();
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, string path)
        {
            timeStamp = default;
            return ResultFs.NotImplemented.Log();
        }

        public Result Commit()
        {
            lock (Locker)
            {
                if (OpenWritableFileCount > 0)
                {
                    // All files must be closed before commiting save data.
                    return ResultFs.WritableFileOpen.Log();
                }

                Result rc = SynchronizeDirectory(SyncDir, WorkingDir);
                if (rc.IsFailure()) return rc;

                rc = BaseFs.DeleteDirectoryRecursively(CommittedDir);
                if (rc.IsFailure()) return rc;

                return BaseFs.RenameDirectory(SyncDir, CommittedDir);
            }
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            return ResultFs.NotImplemented.Log();
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
