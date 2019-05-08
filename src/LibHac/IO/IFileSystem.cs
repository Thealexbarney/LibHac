using System;
using System.IO;

namespace LibHac.IO
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
        /// <exception cref="IOException">An I/O error occurred while creating the directory.</exception>
        void CreateDirectory(string path);

        /// <summary>
        /// Creates or overwrites a file at the specified path.
        /// </summary>
        /// <param name="path">The full path of the file to create.</param>
        /// <param name="size">The initial size of the created file.</param>
        /// <param name="options">Flags to control how the file is created.
        /// Should usually be <see cref="CreateFileOptions.None"/></param>
        /// <exception cref="IOException">An I/O error occurred while creating the file.</exception>
        void CreateFile(string path, long size, CreateFileOptions options);

        /// <summary>
        /// Deletes the specified directory.
        /// </summary>
        /// <param name="path">The full path of the directory to delete.</param>
        /// <exception cref="DirectoryNotFoundException">The specified directory does not exist.</exception>
        /// <exception cref="IOException">An I/O error occurred while deleting the directory.</exception>
        void DeleteDirectory(string path);

        void DeleteDirectoryRecursively(string path);

        void CleanDirectoryRecursively(string path);

        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to delete.</param>
        /// <exception cref="FileNotFoundException">The specified file does not exist.</exception>
        /// <exception cref="IOException">An I/O error occurred while deleting the file.</exception>
        void DeleteFile(string path);

        /// <summary>
        /// Creates an <see cref="IDirectory"/> instance for enumerating the specified directory.
        /// </summary>
        /// <param name="path">The directory's full path.</param>
        /// <param name="mode">Specifies which sub-entries should be enumerated.</param>
        /// <returns>An <see cref="IDirectory"/> instance for the specified directory.</returns>
        /// <exception cref="DirectoryNotFoundException">The specified directory does not exist.</exception>
        /// <exception cref="IOException">An I/O error occurred while opening the directory.</exception>
        IDirectory OpenDirectory(string path, OpenDirectoryMode mode);

        /// <summary>
        /// Opens an <see cref="IFile"/> instance for the specified path.
        /// </summary>
        /// <param name="path">The full path of the file to open.</param>
        /// <param name="mode">Specifies the access permissions of the created <see cref="IFile"/>.</param>
        /// <returns>An <see cref="IFile"/> instance for the specified path.</returns>
        /// <exception cref="FileNotFoundException">The specified file does not exist.</exception>
        /// <exception cref="IOException">An I/O error occurred while deleting the file.</exception>
        IFile OpenFile(string path, OpenMode mode);

        /// <summary>
        /// Renames or moves a directory to a new location.
        /// </summary>
        /// <param name="srcPath">The full path of the directory to rename.</param>
        /// <param name="dstPath">The new full path of the directory.</param>
        /// <exception cref="DirectoryNotFoundException">The specified directory does not exist.</exception>
        /// <exception cref="IOException">An I/O error occurred while deleting the directory.</exception>
        void RenameDirectory(string srcPath, string dstPath);

        /// <summary>
        /// Renames or moves a file to a new location.
        /// </summary>
        /// <param name="srcPath">The full path of the file to rename.</param>
        /// <param name="dstPath">The new full path of the file.</param>
        /// <exception cref="FileNotFoundException">The specified file does not exist.</exception>
        /// <exception cref="IOException">An I/O error occurred while deleting the file.</exception>
        void RenameFile(string srcPath, string dstPath);

        /// <summary>
        /// Determines whether the specified directory exists.
        /// </summary>
        /// <param name="path">The full path of the directory to check.</param>
        /// <returns><see langword="true"/> if the directory exists, otherwise <see langword="false"/>.</returns>
        bool DirectoryExists(string path);

        /// <summary>
        /// Determines whether the specified file exists.
        /// </summary>
        /// <param name="path">The full path of the file to check.</param>
        /// <returns><see langword="true"/> if the file exists, otherwise <see langword="false"/>.</returns>
        bool FileExists(string path);

        /// <summary>
        /// Determines whether the specified path is a file or directory. 
        /// </summary>
        /// <param name="path">The full path to check.</param>
        /// <returns>The <see cref="DirectoryEntryType"/> of the file.</returns>
        /// <exception cref="FileNotFoundException">The specified path does not exist.</exception>
        DirectoryEntryType GetEntryType(string path);

        long GetFreeSpaceSize(string path);

        long GetTotalSpaceSize(string path);

        FileTimeStampRaw GetFileTimeStampRaw(string path);

        /// <summary>
        /// Commits any changes to a transactional file system.
        /// Does nothing if called on a non-transactional file system.
        /// </summary>
        void Commit();

        void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId);
    }

    /// <summary>
    /// Specifies which types of entries are returned when enumerating an <see cref="IDirectory"/>.
    /// </summary>
    [Flags]
    public enum OpenDirectoryMode
    {
        Directories = 1,
        Files = 2,
        All = Directories | Files
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
        MakeConcatFile = 0
    }
}