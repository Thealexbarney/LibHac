using System.Collections.Generic;

namespace LibHac.Fs
{
    /// <summary>
    /// Provides an interface for enumerating the child entries of a directory.
    /// </summary>
    public interface IDirectory
    {
        /// <summary>
        /// The <see cref="IFileSystem"/> that contains the current <see cref="IDirectory"/>.
        /// </summary>
        IFileSystem ParentFileSystem { get; }

        /// <summary>
        /// The full path of the current <see cref="IDirectory"/> in its <see cref="ParentFileSystem"/>.
        /// </summary>
        string FullPath { get; }

        /// <summary>
        /// Specifies which types of entries will be enumerated when <see cref="Read"/> is called.
        /// </summary>
        OpenDirectoryMode Mode { get; }

        /// <summary>
        /// Returns an enumerable collection the file system entries of the types specified by
        /// <see cref="Mode"/> that this directory contains. Does not search subdirectories.
        /// </summary>
        /// <returns>An enumerable collection of file system entries in this directory.</returns>
        IEnumerable<DirectoryEntry> Read();

        /// <summary>
        /// Returns the number of file system entries of the types specified by
        /// <see cref="Mode"/> that this directory contains. Does not search subdirectories.
        /// </summary>
        /// <returns>The number of child entries the directory contains.</returns>
        int GetEntryCount();
    }
}