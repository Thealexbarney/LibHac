using System;

namespace LibHac.Fs
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
                BaseFs.CreateDirectory(CommittedDir);
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

        public void CreateDirectory(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                BaseFs.CreateDirectory(fullPath);
            }
        }

        public void CreateFile(string path, long size, CreateFileOptions options)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                BaseFs.CreateFile(fullPath, size, options);
            }
        }

        public void DeleteDirectory(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                BaseFs.DeleteDirectory(fullPath);
            }
        }

        public void DeleteDirectoryRecursively(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                BaseFs.DeleteDirectoryRecursively(fullPath);
            }
        }

        public void CleanDirectoryRecursively(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                BaseFs.CleanDirectoryRecursively(fullPath);
            }
        }

        public void DeleteFile(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                BaseFs.DeleteFile(fullPath);
            }
        }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.OpenDirectory(fullPath, mode);
            }
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                IFile baseFile = BaseFs.OpenFile(fullPath, mode);
                var file = new DirectorySaveDataFile(this, baseFile);

                if (mode.HasFlag(OpenMode.Write))
                {
                    OpenWritableFileCount++;
                }

                return file;
            }
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            string fullSrcPath = GetFullPath(PathTools.Normalize(srcPath));
            string fullDstPath = GetFullPath(PathTools.Normalize(dstPath));

            lock (Locker)
            {
                BaseFs.RenameDirectory(fullSrcPath, fullDstPath);
            }
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            string fullSrcPath = GetFullPath(PathTools.Normalize(srcPath));
            string fullDstPath = GetFullPath(PathTools.Normalize(dstPath));

            lock (Locker)
            {
                BaseFs.RenameFile(fullSrcPath, fullDstPath);
            }
        }

        public bool DirectoryExists(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.DirectoryExists(fullPath);
            }
        }

        public bool FileExists(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.FileExists(fullPath);
            }
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            string fullPath = GetFullPath(PathTools.Normalize(path));

            lock (Locker)
            {
                return BaseFs.GetEntryType(fullPath);
            }
        }

        public long GetFreeSpaceSize(string path)
        {
            ThrowHelper.ThrowResult(ResultFs.NotImplemented);
            return default;
        }

        public long GetTotalSpaceSize(string path)
        {
            ThrowHelper.ThrowResult(ResultFs.NotImplemented);
            return default;
        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            ThrowHelper.ThrowResult(ResultFs.NotImplemented);
            return default;
        }

        public void Commit()
        {
            if (OpenWritableFileCount > 0)
            {
                ThrowHelper.ThrowResult(ResultFs.WritableFileOpen,
                    "All files must be closed before commiting save data.");
            }

            SynchronizeDirectory(SyncDir, WorkingDir);

            BaseFs.DeleteDirectoryRecursively(CommittedDir);

            BaseFs.RenameDirectory(SyncDir, CommittedDir);
        }

        public void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId)
        {
            ThrowHelper.ThrowResult(ResultFs.NotImplemented);
        }

        private string GetFullPath(string path)
        {
            return PathTools.Normalize(PathTools.Combine(WorkingDir, path));
        }

        private void SynchronizeDirectory(string dest, string src)
        {
            if (BaseFs.DirectoryExists(dest))
            {
                BaseFs.DeleteDirectoryRecursively(dest);
            }

            BaseFs.CreateDirectory(dest);

            IDirectory sourceDir = BaseFs.OpenDirectory(src, OpenDirectoryMode.All);
            IDirectory destDir = BaseFs.OpenDirectory(dest, OpenDirectoryMode.All);

            sourceDir.CopyDirectory(destDir);
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
