using System;
using LibHac.Common;
using LibHac.FsSystem;

namespace LibHac.Fs
{
    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// Provides an interface for accessing a file system. <c>/</c> is used as the path delimiter.
    /// </summary>
    public abstract class IFileSystem : IDisposable
    {
        /// <summary>
        /// Creates or overwrites a file at the specified path.
        /// </summary>
        /// <param name="path">The full path of the file to create.</param>
        /// <param name="size">The initial size of the created file.</param>
        /// <param name="option">Flags to control how the file is created.
        /// Should usually be <see cref="CreateFileOptions.None"/></param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        /// <remarks>
        /// The following <see cref="Result"/> codes may be returned under certain conditions:
        /// 
        /// The parent directory of the specified path does not exist: <see cref="ResultFs.PathNotFound"/>
        /// Specified path already exists as either a file or directory: <see cref="ResultFs.PathAlreadyExists"/>
        /// Insufficient free space to create the file: <see cref="ResultFs.InsufficientFreeSpace"/>
        /// </remarks>
        public Result CreateFile(U8Span path, long size, CreateFileOptions option)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            if (size < 0)
                return ResultFs.OutOfRange.Log();

            return DoCreateFile(path, size, option);
        }

        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to delete.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        /// <remarks>
        /// The following <see cref="Result"/> codes may be returned under certain conditions:
        /// 
        /// The specified path does not exist or is a directory: <see cref="ResultFs.PathNotFound"/>
        /// </remarks>
        public Result DeleteFile(U8Span path)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoDeleteFile(path);
        }

        /// <summary>
        /// Creates all directories and subdirectories in the specified path unless they already exist.
        /// </summary>
        /// <param name="path">The full path of the directory to create.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        /// <remarks>
        /// The following <see cref="Result"/> codes may be returned under certain conditions:
        /// 
        /// The parent directory of the specified path does not exist: <see cref="ResultFs.PathNotFound"/>
        /// Specified path already exists as either a file or directory: <see cref="ResultFs.PathAlreadyExists"/>
        /// Insufficient free space to create the directory: <see cref="ResultFs.InsufficientFreeSpace"/>
        /// </remarks>
        public Result CreateDirectory(U8Span path)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoCreateDirectory(path);
        }

        /// <summary>
        /// Deletes the specified directory.
        /// </summary>
        /// <param name="path">The full path of the directory to delete.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        /// <remarks>
        /// The following <see cref="Result"/> codes may be returned under certain conditions:
        /// 
        /// The specified path does not exist or is a file: <see cref="ResultFs.PathNotFound"/>
        /// The specified directory is not empty: <see cref="ResultFs.DirectoryNotEmpty"/>
        /// </remarks>
        public Result DeleteDirectory(U8Span path)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoDeleteDirectory(path);
        }

        /// <summary>
        /// Deletes the specified directory and any subdirectories and files in the directory.
        /// </summary>
        /// <param name="path">The full path of the directory to delete.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        /// <remarks>
        /// The following <see cref="Result"/> codes may be returned under certain conditions:
        /// 
        /// The specified path does not exist or is a file: <see cref="ResultFs.PathNotFound"/>
        /// </remarks>
        public Result DeleteDirectoryRecursively(U8Span path)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoDeleteDirectoryRecursively(path);
        }

        /// <summary>
        /// Deletes any subdirectories and files in the specified directory.
        /// </summary>
        /// <param name="path">The full path of the directory to clean.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        /// <remarks>
        /// The following <see cref="Result"/> codes may be returned under certain conditions:
        /// 
        /// The specified path does not exist or is a file: <see cref="ResultFs.PathNotFound"/>
        /// </remarks>
        public Result CleanDirectoryRecursively(U8Span path)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoCleanDirectoryRecursively(path);
        }

        /// <summary>
        /// Renames or moves a file to a new location.
        /// </summary>
        /// <param name="oldPath">The full path of the file to rename.</param>
        /// <param name="newPath">The new full path of the file.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        /// <remarks>
        /// If <paramref name="oldPath"/> and <paramref name="newPath"/> are the same, this function does nothing and returns successfully.
        /// The following <see cref="Result"/> codes may be returned under certain conditions:
        /// 
        /// <paramref name="oldPath"/> does not exist or is a directory: <see cref="ResultFs.PathNotFound"/>
        /// <paramref name="newPath"/>'s parent directory does not exist: <see cref="ResultFs.PathNotFound"/>
        /// <paramref name="newPath"/> already exists as either a file or directory: <see cref="ResultFs.PathAlreadyExists"/>
        /// </remarks>
        public Result RenameFile(U8Span oldPath, U8Span newPath)
        {
            if (oldPath.IsNull())
                return ResultFs.NullptrArgument.Log();

            if (newPath.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoRenameFile(oldPath, newPath);
        }

        /// <summary>
        /// Renames or moves a directory to a new location.
        /// </summary>
        /// <param name="oldPath">The full path of the directory to rename.</param>
        /// <param name="newPath">The new full path of the directory.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        /// <remarks>
        /// If <paramref name="oldPath"/> and <paramref name="newPath"/> are the same, this function does nothing and returns <see cref="Result.Success"/>.
        /// The following <see cref="Result"/> codes may be returned under certain conditions:
        /// 
        /// <paramref name="oldPath"/> does not exist or is a file: <see cref="ResultFs.PathNotFound"/>
        /// <paramref name="newPath"/>'s parent directory does not exist: <see cref="ResultFs.PathNotFound"/>
        /// <paramref name="newPath"/> already exists as either a file or directory: <see cref="ResultFs.PathAlreadyExists"/>
        /// Either <paramref name="oldPath"/> or <paramref name="newPath"/> is a subpath of the other: <see cref="ResultFs.DestinationIsSubPathOfSource"/>
        /// </remarks>
        public Result RenameDirectory(U8Span oldPath, U8Span newPath)
        {
            if (oldPath.IsNull())
                return ResultFs.NullptrArgument.Log();

            if (newPath.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoRenameDirectory(oldPath, newPath);
        }

        /// <summary>
        /// Determines whether the specified path is a file or directory, or does not exist.
        /// </summary>
        /// <param name="entryType">If the operation returns successfully, the <see cref="DirectoryEntryType"/> of the file.</param>
        /// <param name="path">The full path to check.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        public Result GetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            if (path.IsNull())
            {
                entryType = default;
                return ResultFs.NullptrArgument.Log();
            }

            return DoGetEntryType(out entryType, path);
        }

        /// <summary>
        /// Gets the amount of available free space on a drive, in bytes.
        /// </summary>
        /// <param name="freeSpace">If the operation returns successfully, the amount of free space available on the drive, in bytes.</param>
        /// <param name="path">The path of the drive to query. Unused in almost all cases.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        public Result GetFreeSpaceSize(out long freeSpace, U8Span path)
        {
            if (path.IsNull())
            {
                freeSpace = default;
                return ResultFs.NullptrArgument.Log();
            }

            return DoGetFreeSpaceSize(out freeSpace, path);
        }

        /// <summary>
        /// Gets the total size of storage space on a drive, in bytes.
        /// </summary>
        /// <param name="totalSpace">If the operation returns successfully, the total size of the drive, in bytes.</param>
        /// <param name="path">The path of the drive to query. Unused in almost all cases.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        public Result GetTotalSpaceSize(out long totalSpace, U8Span path)
        {
            if (path.IsNull())
            {
                totalSpace = default;
                return ResultFs.NullptrArgument.Log();
            }

            return DoGetTotalSpaceSize(out totalSpace, path);
        }

        /// <summary>
        /// Opens an <see cref="IFile"/> instance for the specified path.
        /// </summary>
        /// <param name="file">If the operation returns successfully,
        /// An <see cref="IFile"/> instance for the specified path.</param>
        /// <param name="path">The full path of the file to open.</param>
        /// <param name="mode">Specifies the access permissions of the created <see cref="IFile"/>.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        /// <remarks>
        /// The following <see cref="Result"/> codes may be returned under certain conditions:
        /// 
        /// The specified path does not exist or is a directory: <see cref="ResultFs.PathNotFound"/>
        /// </remarks>
        public Result OpenFile(out IFile file, U8Span path, OpenMode mode)
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

        /// <summary>
        /// Creates an <see cref="IDirectory"/> instance for enumerating the specified directory.
        /// </summary>
        /// <param name="directory">If the operation returns successfully,
        /// An <see cref="IDirectory"/> instance for the specified directory.</param>
        /// <param name="path">The directory's full path.</param>
        /// <param name="mode">Specifies which sub-entries should be enumerated.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        /// <remarks>
        /// The following <see cref="Result"/> codes may be returned under certain conditions:
        /// 
        /// The specified path does not exist or is a file: <see cref="ResultFs.PathNotFound"/>
        /// </remarks>
        public Result OpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
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

        /// <summary>
        /// Commits any changes to a transactional file system.
        /// Does nothing if called on a non-transactional file system.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        public Result Commit() => DoCommit();

        public Result CommitProvisionally(long counter) => DoCommitProvisionally(counter);

        public Result Rollback() => DoRollback();

        public Result Flush() => DoFlush();

        /// <summary>
        /// Gets the creation, last accessed, and last modified timestamps of a file or directory.
        /// </summary>
        /// <param name="timeStamp">If the operation returns successfully, the timestamps for the specified file or directory.
        /// These value are expressed as Unix timestamps.</param>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        /// <remarks>
        /// The following <see cref="Result"/> codes may be returned under certain conditions:
        /// 
        /// The specified path does not exist: <see cref="ResultFs.PathNotFound"/>
        /// </remarks>
        public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
        {
            if (path.IsNull())
            {
                timeStamp = default;
                return ResultFs.NullptrArgument.Log();
            }

            return DoGetFileTimeStampRaw(out timeStamp, path);
        }

        /// <summary>
        /// Performs a query on the specified file.
        /// </summary>
        /// <remarks>This method allows implementers of <see cref="IFileSystem"/> to accept queries and operations
        /// not included in the IFileSystem interface itself.</remarks>
        /// <param name="outBuffer">The buffer for receiving data from the query operation.
        /// May be unused depending on the query type.</param>
        /// <param name="inBuffer">The buffer for sending data to the query operation.
        /// May be unused depending on the query type.</param>
        /// <param name="queryId">The type of query to perform.</param>
        /// <param name="path">The full path of the file to query.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
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

        protected abstract Result DoOpenFile(out IFile file, U8Span path, OpenMode mode);
        protected abstract Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode);
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

    /// <summary>
    /// Specifies which types of entries are returned when enumerating an <see cref="IDirectory"/>.
    /// </summary>
    [Flags]
    public enum OpenDirectoryMode
    {
        Directory = 1 << 0,
        File = 1 << 1,
        NoFileSize = 1 << 31,
        All = Directory | File
    }

    /// <summary>
    /// Optional file creation flags.
    /// </summary>
    [Flags]
    public enum CreateFileOptions
    {
        None = 0,
        /// <summary>
        /// On a <see cref="ConcatenationFileSystem"/>, creates a concatenation file.
        /// </summary>
        CreateConcatenationFile = 1 << 0
    }

    public enum QueryId
    {
        /// <summary>
        /// Turns a folder in a <see cref="ConcatenationFileSystem"/> into a concatenation file by
        /// setting the directory's archive flag.
        /// </summary>
        MakeConcatFile = 0
    }
}
