using System;
using System.Collections.Generic;

namespace LibHac.Fs.Accessors
{
    public class DirectoryAccessor : IDisposable
    {
        private IDirectory Directory { get; set; }

        public FileSystemAccessor Parent { get; }

        public DirectoryAccessor(IDirectory baseDirectory, FileSystemAccessor parent)
        {
            Directory = baseDirectory;
            Parent = parent;
        }

        public IEnumerable<DirectoryEntry> Read()
        {
            CheckIfDisposed();

            return Directory.Read();
        }

        public int GetEntryCount()
        {
            CheckIfDisposed();

            return Directory.GetEntryCount();
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
