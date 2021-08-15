using System;
using LibHac.Common;
using LibHac.Fs.Fsa;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs.Impl
{
    internal class DirectoryAccessor : IDisposable
    {
        private UniqueRef<IDirectory> _directory;
        private FileSystemAccessor _parentFileSystem;

        public DirectoryAccessor(ref UniqueRef<IDirectory> directory, FileSystemAccessor parentFileSystem)
        {
            _directory = new UniqueRef<IDirectory>(ref directory);
            _parentFileSystem = parentFileSystem;
        }

        public void Dispose()
        {
            _directory.Reset();
            _parentFileSystem.NotifyCloseDirectory(this);

            _directory.Destroy();
        }

        public FileSystemAccessor GetParent() => _parentFileSystem;

        public Result Read(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            return _directory.Get.Read(out entriesRead, entryBuffer);
        }

        public Result GetEntryCount(out long entryCount)
        {
            return _directory.Get.GetEntryCount(out entryCount);
        }
    }
}
