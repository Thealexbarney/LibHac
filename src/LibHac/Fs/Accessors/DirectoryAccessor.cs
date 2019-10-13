using System;
using System.Collections.Generic;
using LibHac.FsSystem;

namespace LibHac.Fs.Accessors
{
    public class DirectoryAccessor : IDisposable
    {
        private IDirectory Directory { get; set; }

        public FileSystemAccessor Parent { get; }

        private IFileSystem ParentFs { get; }
        private string Path { get; }

        public DirectoryAccessor(IDirectory baseDirectory, FileSystemAccessor parent, IFileSystem parentFs, string path)
        {
            Directory = baseDirectory;
            Parent = parent;
            ParentFs = parentFs;
            Path = path;
        }

        public IEnumerable<DirectoryEntryEx> Read()
        {
            CheckIfDisposed();

            return ParentFs.EnumerateEntries(Path, "*", SearchOptions.Default);
        }

        public Result Read(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            return Directory.Read(out entriesRead, entryBuffer);
        }

        public Result GetEntryCount(out long entryCount)
        {
            CheckIfDisposed();

            return Directory.GetEntryCount(out entryCount);
        }

        public void Dispose()
        {
            if (Directory == null) return;

            Parent?.NotifyCloseDirectory(this);

            Directory = null;
        }

        private void CheckIfDisposed()
        {
            if (Directory == null) throw new ObjectDisposedException(null, "Cannot access closed directory.");
        }
    }
}
