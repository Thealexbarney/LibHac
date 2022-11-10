using System;
using LibHac.Common;
using LibHac.FsSystem;

namespace LibHac.Fs.Fsa;

// ReSharper disable once InconsistentNaming
/// <summary>
/// Provides an interface for accessing a file system. <c>/</c> is used as the path delimiter.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public abstract class IFileSystem : IDisposable
{
    public virtual void Dispose() { }

    /// <summary>
    /// Creates or overwrites a file at the specified path.
    /// </summary>
    /// <param name="path">The full path of the file to create.</param>
    /// <param name="size">The initial size of the created file.</param>
    /// <param name="option">Flags to control how the file is created.
    /// Should usually be <see cref="CreateFileOptions.None"/></param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The parent directory of the specified path does not exist.<br/>
    /// <see cref="ResultFs.PathAlreadyExists"/>: Specified path already exists as either a file or directory.<br/>
    /// <see cref="ResultFs.UsableSpaceNotEnough"/>: Insufficient free space to create the file.</returns>
    public Result CreateFile(in Path path, long size, CreateFileOptions option)
    {
        if (size < 0)
            return ResultFs.OutOfRange.Log();

        return DoCreateFile(in path, size, option);
    }

    /// <summary>
    /// Creates or overwrites a file at the specified path.
    /// </summary>
    /// <param name="path">The full path of the file to create.</param>
    /// <param name="size">The initial size of the created file.
    /// Should usually be <see cref="CreateFileOptions.None"/></param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The parent directory of the specified path does not exist.<br/>
    /// <see cref="ResultFs.PathAlreadyExists"/>: Specified path already exists as either a file or directory.<br/>
    /// <see cref="ResultFs.UsableSpaceNotEnough"/>: Insufficient free space to create the file.</returns>
    public Result CreateFile(in Path path, long size)
    {
        return CreateFile(in path, size, CreateFileOptions.None);
    }

    /// <summary>
    /// Deletes the specified file.
    /// </summary>
    /// <param name="path">The full path of the file to delete.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The specified path does not exist or is a directory.</returns>
    public Result DeleteFile(in Path path)
    {
        return DoDeleteFile(in path);
    }

    /// <summary>
    /// Creates all directories and subdirectories in the specified path unless they already exist.
    /// </summary>
    /// <param name="path">The full path of the directory to create.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The parent directory of the specified path does not exist.<br/>
    /// <see cref="ResultFs.PathAlreadyExists"/>: Specified path already exists as either a file or directory.<br/>
    /// <see cref="ResultFs.UsableSpaceNotEnough"/>: Insufficient free space to create the directory.</returns>
    public Result CreateDirectory(in Path path)
    {
        return DoCreateDirectory(in path);
    }

    /// <summary>
    /// Deletes the specified directory.
    /// </summary>
    /// <param name="path">The full path of the directory to delete.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The specified path does not exist or is a file.<br/>
    /// <see cref="ResultFs.DirectoryNotEmpty"/>: The specified directory is not empty.</returns>
    public Result DeleteDirectory(in Path path)
    {
        return DoDeleteDirectory(in path);
    }

    /// <summary>
    /// Deletes the specified directory and any subdirectories and files in the directory.
    /// </summary>
    /// <param name="path">The full path of the directory to delete.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The specified path does not exist or is a file.</returns>
    public Result DeleteDirectoryRecursively(in Path path)
    {
        return DoDeleteDirectoryRecursively(in path);
    }

    /// <summary>
    /// Deletes any subdirectories and files in the specified directory.
    /// </summary>
    /// <param name="path">The full path of the directory to clean.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The specified path does not exist or is a file.</returns>
    public Result CleanDirectoryRecursively(in Path path)
    {
        return DoCleanDirectoryRecursively(in path);
    }

    /// <summary>
    /// Renames or moves a file to a new location.
    /// </summary>
    /// <param name="currentPath">The current full path of the file to rename.</param>
    /// <param name="newPath">The new full path of the file.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: <paramref name="currentPath"/> does not exist or is a directory.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: <paramref name="newPath"/>'s parent directory does not exist.<br/>
    /// <see cref="ResultFs.PathAlreadyExists"/>: <paramref name="newPath"/> already exists as either a file or directory.</returns>
    /// <remarks>
    /// If <paramref name="currentPath"/> and <paramref name="newPath"/> are the same, this function does nothing and returns successfully.
    /// </remarks>
    public Result RenameFile(in Path currentPath, in Path newPath)
    {
        return DoRenameFile(in currentPath, in newPath);
    }

    /// <summary>
    /// Renames or moves a directory to a new location.
    /// </summary>
    /// <param name="currentPath">The full path of the directory to rename.</param>
    /// <param name="newPath">The new full path of the directory.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: <paramref name="currentPath"/> does not exist or is a file.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: <paramref name="newPath"/>'s parent directory does not exist.<br/>
    /// <see cref="ResultFs.PathAlreadyExists"/>: <paramref name="newPath"/> already exists as either a file or directory.<br/>
    /// <see cref="ResultFs.DirectoryUnrenamable"/>: Either <paramref name="currentPath"/> or <paramref name="newPath"/> is a subpath of the other.</returns>
    /// <remarks>
    /// If <paramref name="currentPath"/> and <paramref name="newPath"/> are the same, this function does nothing and returns <see cref="Result.Success"/>.
    /// </remarks>
    public Result RenameDirectory(in Path currentPath, in Path newPath)
    {
        return DoRenameDirectory(in currentPath, in newPath);
    }

    /// <summary>
    /// Determines whether the specified path is a file or directory, or does not exist.
    /// </summary>
    /// <param name="entryType">If the operation returns successfully, contains the <see cref="DirectoryEntryType"/> of the file.</param>
    /// <param name="path">The full path to check.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The specified path does not exist.</returns>
    public Result GetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        return DoGetEntryType(out entryType, in path);
    }

    /// <summary>
    /// Determines whether the specified path is a file or directory, or does not exist.
    /// </summary>
    /// <param name="entryType">If the operation returns successfully, contains the <see cref="DirectoryEntryType"/> of the file.</param>
    /// <param name="path">The full path to check.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The specified path does not exist.</returns>
    public Result GetEntryType(out DirectoryEntryType entryType, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out entryType);

        if (path.IsNull())
            return ResultFs.NullptrArgument.Log();

        using var pathNormalized = new Path();
        Result res = pathNormalized.InitializeWithNormalization(path);
        if (res.IsFailure()) return res;

        return DoGetEntryType(out entryType, in pathNormalized);
    }

    /// <summary>
    /// Opens an <see cref="IFile"/> instance for the specified path.
    /// </summary>
    /// <param name="file">If the operation returns successfully,
    /// An <see cref="IFile"/> instance for the specified path.</param>
    /// <param name="path">The full path of the file to open.</param>
    /// <param name="mode">Specifies the access permissions of the created <see cref="IFile"/>.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The specified path does not exist or is a directory.<br/>
    /// <see cref="ResultFs.TargetLocked"/>: When opening as <see cref="OpenMode.Write"/>,
    /// the file is already opened as <see cref="OpenMode.Write"/>.</returns>
    public Result OpenFile(ref UniqueRef<IFile> file, in Path path, OpenMode mode)
    {
        if ((mode & OpenMode.ReadWrite) == 0 || (mode & ~OpenMode.All) != 0)
            return ResultFs.InvalidModeForFileOpen.Log();

        return DoOpenFile(ref file, in path, mode);
    }

    /// <summary>
    /// Opens an <see cref="IFile"/> instance for the specified path.
    /// </summary>
    /// <param name="file">If the operation returns successfully,
    /// An <see cref="IFile"/> instance for the specified path.</param>
    /// <param name="path">The full path of the file to open.</param>
    /// <param name="mode">Specifies the access permissions of the created <see cref="IFile"/>.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The specified path does not exist or is a directory.<br/>
    /// <see cref="ResultFs.TargetLocked"/>: When opening as <see cref="OpenMode.Write"/>,
    /// the file is already opened as <see cref="OpenMode.Write"/>.</returns>
    public Result OpenFile(ref UniqueRef<IFile> file, U8Span path, OpenMode mode)
    {
        if (path.IsNull())
            return ResultFs.NullptrArgument.Log();

        using var pathNormalized = new Path();
        Result res = pathNormalized.InitializeWithNormalization(path);
        if (res.IsFailure()) return res;

        return DoOpenFile(ref file, in pathNormalized, mode);
    }

    /// <summary>
    /// Creates an <see cref="IDirectory"/> instance for enumerating the specified directory.
    /// </summary>
    /// <param name="outDirectory"></param>
    /// <param name="path">The directory's full path.</param>
    /// <param name="mode">Specifies which sub-entries should be enumerated.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The specified path does not exist or is a file.</returns>
    public Result OpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path, OpenDirectoryMode mode)
    {
        if ((mode & OpenDirectoryMode.All) == 0 ||
            (mode & ~(OpenDirectoryMode.All | OpenDirectoryMode.NoFileSize)) != 0)
            return ResultFs.InvalidModeForFileOpen.Log();

        return DoOpenDirectory(ref outDirectory, in path, mode);
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
    /// Gets the amount of available free space on a drive, in bytes.
    /// </summary>
    /// <param name="freeSpace">If the operation returns successfully, the amount of free space available on the drive, in bytes.</param>
    /// <param name="path">The path of the drive to query. Unused in almost all cases.</param>
    /// <returns>The <see cref="Result"/> of the requested operation.</returns>
    public Result GetFreeSpaceSize(out long freeSpace, in Path path)
    {
        return DoGetFreeSpaceSize(out freeSpace, in path);
    }

    /// <summary>
    /// Gets the total size of storage space on a drive, in bytes.
    /// </summary>
    /// <param name="totalSpace">If the operation returns successfully, the total size of the drive, in bytes.</param>
    /// <param name="path">The path of the drive to query. Unused in almost all cases.</param>
    /// <returns>The <see cref="Result"/> of the requested operation.</returns>
    public Result GetTotalSpaceSize(out long totalSpace, in Path path)
    {
        return DoGetTotalSpaceSize(out totalSpace, in path);
    }

    /// <summary>
    /// Gets the creation, last accessed, and last modified timestamps of a file or directory.
    /// </summary>
    /// <param name="timeStamp">If the operation returns successfully, the timestamps for the specified file or directory.
    /// These value are expressed as Unix timestamps.</param>
    /// <param name="path">The path of the file or directory.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The specified path does not exist.</returns>
    public Result GetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
    {
        return DoGetFileTimeStampRaw(out timeStamp, in path);
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
    public Result QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, in Path path)
    {
        return DoQueryEntry(outBuffer, inBuffer, queryId, path);
    }

    protected abstract Result DoCreateFile(in Path path, long size, CreateFileOptions option);
    protected abstract Result DoDeleteFile(in Path path);
    protected abstract Result DoCreateDirectory(in Path path);
    protected abstract Result DoDeleteDirectory(in Path path);
    protected abstract Result DoDeleteDirectoryRecursively(in Path path);
    protected abstract Result DoCleanDirectoryRecursively(in Path path);
    protected abstract Result DoRenameFile(in Path currentPath, in Path newPath);
    protected abstract Result DoRenameDirectory(in Path currentPath, in Path newPath);
    protected abstract Result DoGetEntryType(out DirectoryEntryType entryType, in Path path);

    protected virtual Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out freeSpace);
        return ResultFs.NotImplemented.Log();
    }

    protected virtual Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out totalSpace);
        return ResultFs.NotImplemented.Log();
    }

    protected abstract Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode);
    protected abstract Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode);
    protected abstract Result DoCommit();

    protected virtual Result DoCommitProvisionally(long counter) => ResultFs.NotImplemented.Log();
    protected virtual Result DoRollback() => ResultFs.NotImplemented.Log();
    protected virtual Result DoFlush() => ResultFs.NotImplemented.Log();

    protected virtual Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out timeStamp);
        return ResultFs.NotImplemented.Log();
    }

    protected virtual Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
        in Path path) => ResultFs.NotImplemented.Log();
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
    SetConcatenationFileAttribute = 0,
    UpdateMac = 1,
    IsSignedSystemPartition = 2,
    QueryUnpreparedFileInformation = 3
}