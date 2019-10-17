using System;
using System.Threading;

namespace LibHac.Fs
{
    public abstract class FileSystemBase : IFileSystem
    {
        // 0 = not disposed; 1 = disposed
        private int _disposedState;
        protected bool IsDisposed => _disposedState != 0;

        protected abstract Result CreateDirectoryImpl(string path);
        protected abstract Result CreateFileImpl(string path, long size, CreateFileOptions options);
        protected abstract Result DeleteDirectoryImpl(string path);
        protected abstract Result DeleteDirectoryRecursivelyImpl(string path);
        protected abstract Result CleanDirectoryRecursivelyImpl(string path);
        protected abstract Result DeleteFileImpl(string path);
        protected abstract Result OpenDirectoryImpl(out IDirectory directory, string path, OpenDirectoryMode mode);
        protected abstract Result OpenFileImpl(out IFile file, string path, OpenMode mode);
        protected abstract Result RenameDirectoryImpl(string oldPath, string newPath);
        protected abstract Result RenameFileImpl(string oldPath, string newPath);
        protected abstract Result GetEntryTypeImpl(out DirectoryEntryType entryType, string path);
        protected abstract Result CommitImpl();

        protected virtual Result GetFreeSpaceSizeImpl(out long freeSpace, string path)
        {
            freeSpace = default;
            return ResultFs.NotImplemented.Log();
        }

        protected virtual Result GetTotalSpaceSizeImpl(out long totalSpace, string path)
        {
            totalSpace = default;
            return ResultFs.NotImplemented.Log();
        }

        protected virtual Result GetFileTimeStampRawImpl(out FileTimeStampRaw timeStamp, string path)
        {
            timeStamp = default;
            return ResultFs.NotImplemented.Log();
        }

        protected virtual Result QueryEntryImpl(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            return ResultFs.NotImplemented.Log();
        }

        public Result CreateDirectory(string path)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return CreateDirectoryImpl(path);
        }

        public Result CreateFile(string path, long size, CreateFileOptions options)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return CreateFileImpl(path, size, options);
        }

        public Result DeleteDirectory(string path)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return DeleteDirectoryImpl(path);
        }

        public Result DeleteDirectoryRecursively(string path)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return DeleteDirectoryRecursivelyImpl(path);
        }

        public Result CleanDirectoryRecursively(string path)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return CleanDirectoryRecursivelyImpl(path);
        }

        public Result DeleteFile(string path)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return DeleteFileImpl(path);
        }

        public Result OpenDirectory(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            if (IsDisposed)
            {
                directory = default;
                return ResultFs.PreconditionViolation.Log();
            }

            return OpenDirectoryImpl(out directory, path, mode);
        }

        public Result OpenFile(out IFile file, string path, OpenMode mode)
        {
            if (IsDisposed)
            {
                file = default;
                return ResultFs.PreconditionViolation.Log();
            }

            return OpenFileImpl(out file, path, mode);
        }

        public Result RenameDirectory(string oldPath, string newPath)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return RenameDirectoryImpl(oldPath, newPath);
        }

        public Result RenameFile(string oldPath, string newPath)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return RenameFileImpl(oldPath, newPath);
        }

        public Result GetEntryType(out DirectoryEntryType entryType, string path)
        {
            if (IsDisposed)
            {
                entryType = default;
                return ResultFs.PreconditionViolation.Log();
            }

            return GetEntryTypeImpl(out entryType, path);
        }

        public Result GetFreeSpaceSize(out long freeSpace, string path)
        {
            if (IsDisposed)
            {
                freeSpace = default;
                return ResultFs.PreconditionViolation.Log();
            }

            return GetFreeSpaceSizeImpl(out freeSpace, path);
        }

        public Result GetTotalSpaceSize(out long totalSpace, string path)
        {
            if (IsDisposed)
            {
                totalSpace = default;
                return ResultFs.PreconditionViolation.Log();
            }

            return GetTotalSpaceSizeImpl(out totalSpace, path);
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, string path)
        {
            if (IsDisposed)
            {
                timeStamp = default;
                return ResultFs.PreconditionViolation.Log();
            }

            return GetFileTimeStampRawImpl(out timeStamp, path);
        }

        public Result Commit()
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return CommitImpl();
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return QueryEntryImpl(outBuffer, inBuffer, queryId, path);
        }

        public void Dispose()
        {
            // Make sure Dispose is only called once
            if (Interlocked.CompareExchange(ref _disposedState, 1, 0) == 0)
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        protected virtual void Dispose(bool disposing) { }
    }
}
