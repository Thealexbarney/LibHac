using System;

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
