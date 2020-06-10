using System;

namespace LibHac.Fs.Fsa
{
    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// Provides an interface for enumerating the child entries of a directory.
    /// </summary>
    public abstract class IDirectory : IDisposable
    {
        /// <summary>
        /// Retrieves the next entries that this directory contains. Does not search subdirectories.
        /// </summary>
        /// <param name="entriesRead">The number of <see cref="DirectoryEntry"/>s that
        /// were read into <paramref name="entryBuffer"/>.</param>
        /// <param name="entryBuffer">The buffer the entries will be read into.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        /// <remarks>With each call of <see cref="Read"/>, the <see cref="IDirectory"/> object will
        /// continue to iterate through all the entries it contains.
        /// Each call will attempt to read as many entries as the buffer can contain.
        /// Once all the entries have been read, all subsequent calls to <see cref="Read"/> will
        /// read 0 entries into the buffer.</remarks>
        public Result Read(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            if (entryBuffer.IsEmpty)
            {
                entriesRead = 0;
                return Result.Success;
            }

            return DoRead(out entriesRead, entryBuffer);
        }

        /// <summary>
        /// Retrieves the number of file system entries that this directory contains. Does not search subdirectories.
        /// </summary>
        /// <param name="entryCount">The number of child entries the directory contains.</param>
        /// <returns>The <see cref="Result"/> of the requested operation.</returns>
        public Result GetEntryCount(out long entryCount)
        {
            return DoGetEntryCount(out entryCount);
        }

        protected abstract Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer);
        protected abstract Result DoGetEntryCount(out long entryCount);

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
