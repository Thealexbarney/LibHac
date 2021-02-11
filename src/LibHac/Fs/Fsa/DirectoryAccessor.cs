using System;
using LibHac.Common;
using LibHac.Fs.Fsa;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs.Impl
{
    internal class DirectoryAccessor : IDisposable
    {
        private IDirectory _directory;
        private FileSystemAccessor _parentFileSystem;

        public DirectoryAccessor(ref IDirectory directory, FileSystemAccessor parentFileSystem)
        {
            _directory = Shared.Move(ref directory);
            _parentFileSystem = parentFileSystem;
        }

        public void Dispose()
        {
            _directory?.Dispose();
            _directory = null;

            _parentFileSystem.NotifyCloseDirectory(this);
        }

        public FileSystemAccessor GetParent() => _parentFileSystem;

        public Result Read(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            return _directory.Read(out entriesRead, entryBuffer);
        }

        public Result GetEntryCount(out long entryCount)
        {
            return _directory.GetEntryCount(out entryCount);
        }
    }
}
