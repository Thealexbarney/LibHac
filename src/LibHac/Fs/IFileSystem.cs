using System;

namespace LibHac.Fs
{
    /// <summary>
    /// Provides an interface for accessing a file system. <c>/</c> is used as the path delimiter.
    /// </summary>
    public interface IFileSystem
    {
        /// <summary>
        /// Creates all directories and subdirectories in the specified path unless they already exist.
        /// </summary>
        /// <param name="path">The full path of the directory to create.</param>
        /// <remarks>
        /// A <see cref="HorizonResultException"/> will be thrown with the given <see cref="Result"/> under the following conditions:
        /// 
        /// The parent directory of the specified path does not exist: <see cref="ResultFs.PathNotFound"/>
        /// Specified path already exists as either a file or directory: <see cref="ResultFs.PathAlreadyExists"/>
        /// Insufficient free space to create the directory: <see cref="ResultFs.InsufficientFreeSpace"/>
        /// </remarks>
        void CreateDirectory(string path);

        /// <summary>
        /// Creates or overwrites a file at the specified path.
        /// </summary>
        /// <param name="path">The full path of the file to create.</param>
        /// <param name="size">The initial size of the created file.</param>
        /// <param name="options">Flags to control how the file is created.
        /// Should usually be <see cref="CreateFileOptions.None"/></param>
        /// <remarks>
        /// A <see cref="HorizonResultException"/> will be thrown with the given <see cref="Result"/> under the following conditions:
        /// 
        /// The parent directory of the specified path does not exist: <see cref="ResultFs.PathNotFound"/>
        /// Specified path already exists as either a file or directory: <see cref="ResultFs.PathAlreadyExists"/>
        /// Insufficient free space to create the file: <see cref="ResultFs.InsufficientFreeSpace"/>
        /// </remarks>
        void CreateFile(string path, long size, CreateFileOptions options);

        /// <summary>
        /// Deletes the specified directory.
        /// </summary>
        /// <param name="path">The full path of the directory to delete.</param>
        /// <remarks>
        /// A <see cref="HorizonResultException"/> will be thrown with the given <see cref="Result"/> under the following conditions:
        /// 
        /// The specified path does not exist or is a file: <see cref="ResultFs.PathNotFound"/>
        /// The specified directory is not empty: <see cref="ResultFs.DirectoryNotEmpty"/>
        /// </remarks>
        void DeleteDirectory(string path);

        /// <summary>
        /// Deletes the specified directory and any subdirectories and files in the directory.
        /// </summary>
        /// <param name="path">The full path of the directory to delete.</param>
        /// <remarks>
        /// A <see cref="HorizonResultException"/> will be thrown with the given <see cref="Result"/> under the following conditions:
        /// 
        /// The specified path does not exist or is a file: <see cref="ResultFs.PathNotFound"/>
        /// </remarks>
        void DeleteDirectoryRecursively(string path);

        /// <summary>
        /// Deletes any subdirectories and files in the specified directory.
        /// </summary>
        /// <param name="path">The full path of the directory to clean.</param>
        /// <remarks>
        /// A <see cref="HorizonResultException"/> will be thrown with the given <see cref="Result"/> under the following conditions:
        /// 
        /// The specified path does not exist or is a file: <see cref="ResultFs.PathNotFound"/>
        /// </remarks>
        void CleanDirectoryRecursively(string path);

        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to delete.</param>
        /// <remarks>
        /// A <see cref="HorizonResultException"/> will be thrown with the given <see cref="Result"/> under the following conditions:
        /// 
        /// The specified path does not exist or is a directory: <see cref="ResultFs.PathNotFound"/>
        /// </remarks>
        void DeleteFile(string path);

        /// <summary>
        /// Creates an <see cref="IDirectory"/> instance for enumerating the specified directory.
        /// </summary>
        /// <param name="path">The directory's full path.</param>
        /// <param name="mode">Specifies which sub-entries should be enumerated.</param>
        /// <returns>An <see cref="IDirectory"/> instance for the specified directory.</returns>
        /// <remarks>
        /// A <see cref="HorizonResultException"/> will be thrown with the given <see cref="Result"/> under the following conditions:
        /// 
        /// The specified path does not exist or is a file: <see cref="ResultFs.PathNotFound"/>
        /// </remarks>
        IDirectory OpenDirectory(string path, OpenDirectoryMode mode);

        /// <summary>
        /// Opens an <see cref="IFile"/> instance for the specified path.
        /// </summary>
        /// <param name="path">The full path of the file to open.</param>
        /// <param name="mode">Specifies the access permissions of the created <see cref="IFile"/>.</param>
        /// <returns>An <see cref="IFile"/> instance for the specified path.</returns>
        /// <remarks>
        /// A <see cref="HorizonResultException"/> will be thrown with the given <see cref="Result"/> under the following conditions:
        /// 
        /// The specified path does not exist or is a directory: <see cref="ResultFs.PathNotFound"/>
        /// </remarks>
        IFile OpenFile(string path, OpenMode mode);

        /// <summary>
        /// Renames or moves a directory to a new location.
        /// </summary>
        /// <param name="srcPath">The full path of the directory to rename.</param>
        /// <param name="dstPath">The new full path of the directory.</param>
        /// <returns>An <see cref="IFile"/> instance for the specified path.</returns>
        /// <remarks>
        /// If <paramref name="srcPath"/> and <paramref name="dstPath"/> are the same, this function does nothing and returns successfully.
        /// A <see cref="HorizonResultException"/> will be thrown with the given <see cref="Result"/> under the following conditions:
        /// 
        /// <paramref name="srcPath"/> does not exist or is a file: <see cref="ResultFs.PathNotFound"/>
        /// <paramref name="dstPath"/>'s parent directory does not exist: <see cref="ResultFs.PathNotFound"/>
        /// <paramref name="dstPath"/> already exists as either a file or directory: <see cref="ResultFs.PathAlreadyExists"/>
        /// Either <paramref name="srcPath"/> or <paramref name="dstPath"/> is a subpath of the other: <see cref="ResultFs.DestinationIsSubPathOfSource"/>
        /// </remarks>
        void RenameDirectory(string srcPath, string dstPath);

        /// <summary>
        /// Renames or moves a file to a new location.
        /// </summary>
        /// <param name="srcPath">The full path of the file to rename.</param>
        /// <param name="dstPath">The new full path of the file.</param>
        /// <remarks>
        /// If <paramref name="srcPath"/> and <paramref name="dstPath"/> are the same, this function does nothing and returns successfully.
        /// A <see cref="HorizonResultException"/> will be thrown with the given <see cref="Result"/> under the following conditions:
        /// 
        /// <paramref name="srcPath"/> does not exist or is a directory: <see cref="ResultFs.PathNotFound"/>
        /// <paramref name="dstPath"/>'s parent directory does not exist: <see cref="ResultFs.PathNotFound"/>
        /// <paramref name="dstPath"/> already exists as either a file or directory: <see cref="ResultFs.PathAlreadyExists"/>
        /// </remarks>
        void RenameFile(string srcPath, string dstPath);

        /// <summary>
        /// Determines whether the specified path is a file or directory, or does not exist.
        /// </summary>
        /// <param name="path">The full path to check.</param>
        /// <returns>The <see cref="DirectoryEntryType"/> of the file.</returns>
        /// <remarks>
        /// This function operates slightly differently than it does in Horizon OS.
        /// Instead of returning <see cref="ResultFs.PathNotFound"/> when an entry is missing,
        /// the function will return <see cref="DirectoryEntryType.NotFound"/>.
        /// </remarks>
        DirectoryEntryType GetEntryType(string path);

        /// <summary>
        /// Gets the amount of available free space on a drive, in bytes.
        /// </summary>
        /// <param name="path">The path of the drive to query. Unused in almost all cases.</param>
        /// <returns>The amount of free space available on the drive, in bytes.</returns>
        long GetFreeSpaceSize(string path);

        /// <summary>
        /// Gets the total size of storage space on a drive, in bytes.
        /// </summary>
        /// <param name="path">The path of the drive to query. Unused in almost all cases.</param>
        /// <returns>The total size of the drive, in bytes.</returns>
        long GetTotalSpaceSize(string path);

        /// <summary>
        /// Gets the creation, last accessed, and last modified timestamps of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The timestamps for the specified file or directory.
        /// This value is expressed as a Unix timestamp</returns>
        /// <remarks>
        /// A <see cref="HorizonResultException"/> will be thrown with the given <see cref="Result"/> under the following conditions:
        /// 
        /// The specified path does not exist: <see cref="ResultFs.PathNotFound"/>
        /// </remarks>
        FileTimeStampRaw GetFileTimeStampRaw(string path);

        /// <summary>
        /// Commits any changes to a transactional file system.
        /// Does nothing if called on a non-transactional file system.
        /// </summary>
        void Commit();

        /// <summary>
        /// Performs a query on the specified file.
        /// </summary>
        /// <remarks>This method allows implementers of <see cref="IFileSystem"/> to accept queries and operations
        /// not included in the IFileSystem interface itself.</remarks>
        /// <param name="outBuffer">The buffer for receiving data from the query operation.
        /// May be unused depending on the query type.</param>
        /// <param name="inBuffer">The buffer for sending data to the query operation.
        /// May be unused depending on the query type.</param>
        /// <param name="path">The full path of the file to query.</param>
        /// <param name="queryId">The type of query to perform.</param>
        void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId);
    }

    /// <summary>
    /// Specifies which types of entries are returned when enumerating an <see cref="IDirectory"/>.
    /// </summary>
    [Flags]
    public enum OpenDirectoryMode
    {
        Directory = 1,
        File = 2,
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