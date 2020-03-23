using System;
using System.Threading;
using LibHac.Common;

namespace LibHac.Fs
{
    public abstract class FileSystemBase : IFileSystem
    {
        // 0 = not disposed; 1 = disposed
        private int _disposedState;
        protected bool IsDisposed => _disposedState != 0;

        protected abstract Result CreateDirectoryImpl(U8Span path);
        protected abstract Result CreateFileImpl(U8Span path, long size, CreateFileOptions options);
        protected abstract Result DeleteDirectoryImpl(U8Span path);
        protected abstract Result DeleteDirectoryRecursivelyImpl(U8Span path);
        protected abstract Result CleanDirectoryRecursivelyImpl(U8Span path);
        protected abstract Result DeleteFileImpl(U8Span path);
        protected abstract Result OpenDirectoryImpl(out IDirectory directory, U8Span path, OpenDirectoryMode mode);
        protected abstract Result OpenFileImpl(out IFile file, U8Span path, OpenMode mode);
        protected abstract Result RenameDirectoryImpl(U8Span oldPath, U8Span newPath);
        protected abstract Result RenameFileImpl(U8Span oldPath, U8Span newPath);
        protected abstract Result GetEntryTypeImpl(out DirectoryEntryType entryType, U8Span path);
        protected abstract Result CommitImpl();

        protected virtual Result GetFreeSpaceSizeImpl(out long freeSpace, U8Span path)
        {
            freeSpace = default;
            return ResultFs.NotImplemented.Log();
        }

        protected virtual Result GetTotalSpaceSizeImpl(out long totalSpace, U8Span path)
        {
            totalSpace = default;
            return ResultFs.NotImplemented.Log();
        }

        protected virtual Result CommitProvisionallyImpl(long commitCount)
        {
            return ResultFs.NotImplemented.Log();
        }

        protected virtual Result RollbackImpl()
        {
            return ResultFs.NotImplemented.Log();
        }

        protected virtual Result FlushImpl()
        {
            return ResultFs.NotImplemented.Log();
        }

        protected virtual Result GetFileTimeStampRawImpl(out FileTimeStampRaw timeStamp, U8Span path)
        {
            timeStamp = default;
            return ResultFs.NotImplemented.Log();
        }

        protected virtual Result QueryEntryImpl(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            U8Span path)
        {
            return ResultFs.NotImplemented.Log();
        }

        public Result CreateDirectory(U8Span path)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return CreateDirectoryImpl(path);
        }

        public Result CreateFile(U8Span path, long size, CreateFileOptions options)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return CreateFileImpl(path, size, options);
        }

        public Result DeleteDirectory(U8Span path)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return DeleteDirectoryImpl(path);
        }

        public Result DeleteDirectoryRecursively(U8Span path)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return DeleteDirectoryRecursivelyImpl(path);
        }

        public Result CleanDirectoryRecursively(U8Span path)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return CleanDirectoryRecursivelyImpl(path);
        }

        public Result DeleteFile(U8Span path)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return DeleteFileImpl(path);
        }

        public Result OpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            if (IsDisposed)
            {
                directory = default;
                return ResultFs.PreconditionViolation.Log();
            }

            if (path.IsNull())
            {
                directory = default;
                return ResultFs.NullptrArgument.Log();
            }

            if ((mode & ~OpenDirectoryMode.All) != 0 || (mode & OpenDirectoryMode.All) == 0)
            {
                directory = default;
                return ResultFs.InvalidArgument.Log();
            }

            return OpenDirectoryImpl(out directory, path, mode);
        }

        public Result OpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            if (IsDisposed)
            {
                file = default;
                return ResultFs.PreconditionViolation.Log();
            }

            if (path.IsNull())
            {
                file = default;
                return ResultFs.NullptrArgument.Log();
            }

            if ((mode & ~OpenMode.All) != 0 || (mode & OpenMode.ReadWrite) == 0)
            {
                file = default;
                return ResultFs.InvalidArgument.Log();
            }

            return OpenFileImpl(out file, path, mode);
        }

        public Result RenameDirectory(U8Span oldPath, U8Span newPath)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return RenameDirectoryImpl(oldPath, newPath);
        }

        public Result RenameFile(U8Span oldPath, U8Span newPath)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return RenameFileImpl(oldPath, newPath);
        }

        public Result GetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            if (IsDisposed)
            {
                entryType = default;
                return ResultFs.PreconditionViolation.Log();
            }

            return GetEntryTypeImpl(out entryType, path);
        }

        public Result GetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            if (IsDisposed)
            {
                freeSpace = default;
                return ResultFs.PreconditionViolation.Log();
            }

            return GetFreeSpaceSizeImpl(out freeSpace, path);
        }

        public Result GetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            if (IsDisposed)
            {
                totalSpace = default;
                return ResultFs.PreconditionViolation.Log();
            }

            return GetTotalSpaceSizeImpl(out totalSpace, path);
        }

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
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

        public Result CommitProvisionally(long commitCount)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return CommitProvisionallyImpl(commitCount);
        }

        public Result Rollback()
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return RollbackImpl();
        }

        public Result Flush()
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return FlushImpl();
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, U8Span path)
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
