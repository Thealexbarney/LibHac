using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl
{
    internal class OpenCountFileSystem : ForwardingFileSystem
    {
        private ReferenceCountedDisposable<IEntryOpenCountSemaphoreManager> _entryCountSemaphore;
        private IUniqueLock _mountCountSemaphore;

        protected OpenCountFileSystem(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem,
            ref ReferenceCountedDisposable<IEntryOpenCountSemaphoreManager> entryCountSemaphore) : base(
            ref baseFileSystem)
        {
            Shared.Move(out _entryCountSemaphore, ref entryCountSemaphore);
        }

        protected OpenCountFileSystem(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem,
            ref ReferenceCountedDisposable<IEntryOpenCountSemaphoreManager> entryCountSemaphore,
            ref IUniqueLock mountCountSemaphore) : base(ref baseFileSystem)
        {
            Shared.Move(out _entryCountSemaphore, ref entryCountSemaphore);
            Shared.Move(out _mountCountSemaphore, ref mountCountSemaphore);
        }

        public static ReferenceCountedDisposable<IFileSystem> CreateShared(
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem,
            ref ReferenceCountedDisposable<IEntryOpenCountSemaphoreManager> entryCountSemaphore,
            ref IUniqueLock mountCountSemaphore)
        {
            var filesystem =
                new OpenCountFileSystem(ref baseFileSystem, ref entryCountSemaphore, ref mountCountSemaphore);

            return new ReferenceCountedDisposable<IFileSystem>(filesystem);
        }

        public static ReferenceCountedDisposable<IFileSystem> CreateShared(
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem,
            ref ReferenceCountedDisposable<IEntryOpenCountSemaphoreManager> entryCountSemaphore)
        {
            var filesystem =
                new OpenCountFileSystem(ref baseFileSystem, ref entryCountSemaphore);

            return new ReferenceCountedDisposable<IFileSystem>(filesystem);
        }

        // ReSharper disable once RedundantOverriddenMember
        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            // Todo: Implement
            return base.DoOpenFile(out file, path, mode);
        }

        // ReSharper disable once RedundantOverriddenMember
        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            // Todo: Implement
            return base.DoOpenDirectory(out directory, path, mode);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _entryCountSemaphore?.Dispose();
                _mountCountSemaphore?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
