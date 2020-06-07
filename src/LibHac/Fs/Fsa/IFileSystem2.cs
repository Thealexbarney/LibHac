using System;
using LibHac.Common;

namespace LibHac.Fs.Fsa
{
    // ReSharper disable once InconsistentNaming
    public abstract class IFileSystem2 : IDisposable
    {
        public Result CreateFile(U8Span path, long size, CreateFileOptions option)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            if (size < 0)
                return ResultFs.OutOfRange.Log();

            return DoCreateFile(path, size, option);
        }

        public Result DeleteFile(U8Span path)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoDeleteFile(path);
        }

        public Result CreateDirectory(U8Span path)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoCreateDirectory(path);
        }

        public Result DeleteDirectory(U8Span path)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoDeleteDirectory(path);
        }

        public Result DeleteDirectoryRecursively(U8Span path)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoDeleteDirectoryRecursively(path);
        }

        public Result CleanDirectoryRecursively(U8Span path)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoCleanDirectoryRecursively(path);
        }

        public Result RenameFile(U8Span oldPath, U8Span newPath)
        {
            if (oldPath.IsNull())
                return ResultFs.NullptrArgument.Log();

            if (newPath.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoRenameFile(oldPath, newPath);
        }

        public Result RenameDirectory(U8Span oldPath, U8Span newPath)
        {
            if (oldPath.IsNull())
                return ResultFs.NullptrArgument.Log();

            if (newPath.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoRenameDirectory(oldPath, newPath);
        }

        public Result GetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            if (path.IsNull())
            {
                entryType = default;
                return ResultFs.NullptrArgument.Log();
            }

            return DoGetEntryType(out entryType, path);
        }

        public Result GetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            if (path.IsNull())
            {
                freeSpace = default;
                return ResultFs.NullptrArgument.Log();
            }

            return DoGetFreeSpaceSize(out freeSpace, path);
        }

        public Result GetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            if (path.IsNull())
            {
                totalSpace = default;
                return ResultFs.NullptrArgument.Log();
            }

            return DoGetTotalSpaceSize(out totalSpace, path);
        }

        public Result OpenFile(out IFile2 file, U8Span path, OpenMode mode)
        {
            if (path.IsNull())
            {
                file = default;
                return ResultFs.NullptrArgument.Log();
            }

            if ((mode & OpenMode.ReadWrite) == 0)
            {
                file = default;
                return ResultFs.InvalidOpenMode.Log();
            }

            if ((mode & ~OpenMode.All) != 0)
            {
                file = default;
                return ResultFs.InvalidOpenMode.Log();
            }

            return DoOpenFile(out file, path, mode);
        }

        public Result OpenDirectory(out IDirectory2 directory, U8Span path, OpenDirectoryMode mode)
        {
            if (path.IsNull())
            {
                directory = default;
                return ResultFs.NullptrArgument.Log();
            }

            if ((mode & OpenDirectoryMode.All) == 0)
            {
                directory = default;
                return ResultFs.InvalidOpenMode.Log();
            }

            if ((mode & ~(OpenDirectoryMode.All | OpenDirectoryMode.NoFileSize)) != 0)
            {
                directory = default;
                return ResultFs.InvalidOpenMode.Log();
            }

            return DoOpenDirectory(out directory, path, mode);
        }

        public Result Commit() => DoCommit();

        public Result CommitProvisionally(long counter) => DoCommitProvisionally(counter);

        public Result Rollback() => DoRollback();

        public Result Flush() => DoFlush();

        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
        {
            if (path.IsNull())
            {
                timeStamp = default;
                return ResultFs.NullptrArgument.Log();
            }

            return DoGetFileTimeStampRaw(out timeStamp, path);
        }

        public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, U8Span path)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoQueryEntry(outBuffer, inBuffer, queryId, path);
        }

        protected abstract Result DoCreateFile(U8Span path, long size, CreateFileOptions option);
        protected abstract Result DoDeleteFile(U8Span path);
        protected abstract Result DoCreateDirectory(U8Span path);
        protected abstract Result DoDeleteDirectory(U8Span path);
        protected abstract Result DoDeleteDirectoryRecursively(U8Span path);
        protected abstract Result DoCleanDirectoryRecursively(U8Span path);
        protected abstract Result DoRenameFile(U8Span oldPath, U8Span newPath);
        protected abstract Result DoRenameDirectory(U8Span oldPath, U8Span newPath);
        protected abstract Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path);

        protected virtual Result DoGetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            freeSpace = default;
            return ResultFs.NotImplemented.Log();
        }

        protected virtual Result DoGetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            totalSpace = default;
            return ResultFs.NotImplemented.Log();
        }

        protected abstract Result DoOpenFile(out IFile2 file, U8Span path, OpenMode mode);
        protected abstract Result DoOpenDirectory(out IDirectory2 directory, U8Span path, OpenDirectoryMode mode);
        protected abstract Result DoCommit();

        protected virtual Result DoCommitProvisionally(long counter) => ResultFs.NotImplemented.Log();
        protected virtual Result DoRollback() => ResultFs.NotImplemented.Log();
        protected virtual Result DoFlush() => ResultFs.NotImplemented.Log();

        protected virtual Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
        {
            timeStamp = default;
            return ResultFs.NotImplemented.Log();
        }

        protected virtual Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            U8Span path) => ResultFs.NotImplemented.Log();

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
