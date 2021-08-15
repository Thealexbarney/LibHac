using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl
{
    internal class OpenCountFileSystem : ForwardingFileSystem
    {
        private ReferenceCountedDisposable<IEntryOpenCountSemaphoreManager> _entryCountSemaphore;
        private UniqueRef<IUniqueLock> _mountCountSemaphore;

        public OpenCountFileSystem(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem,
            ref ReferenceCountedDisposable<IEntryOpenCountSemaphoreManager> entryCountSemaphore) : base(
            ref baseFileSystem)
        {
            Shared.Move(out _entryCountSemaphore, ref entryCountSemaphore);
        }

        public OpenCountFileSystem(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem,
            ref ReferenceCountedDisposable<IEntryOpenCountSemaphoreManager> entryCountSemaphore,
            ref UniqueRef<IUniqueLock> mountCountSemaphore) : base(ref baseFileSystem)
        {
            Shared.Move(out _entryCountSemaphore, ref entryCountSemaphore);
            _mountCountSemaphore = new UniqueRef<IUniqueLock>(ref mountCountSemaphore);
        }

        public static ReferenceCountedDisposable<IFileSystem> CreateShared(
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem,
            ref ReferenceCountedDisposable<IEntryOpenCountSemaphoreManager> entryCountSemaphore,
            ref UniqueRef<IUniqueLock> mountCountSemaphore)
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
        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
        {
            // Todo: Implement
            return base.DoOpenFile(ref outFile, path, mode);
        }

        // ReSharper disable once RedundantOverriddenMember
        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
            OpenDirectoryMode mode)
        {
            // Todo: Implement
            return base.DoOpenDirectory(ref outDirectory, path, mode);
        }

        public override void Dispose()
        {
            _entryCountSemaphore?.Dispose();
            _mountCountSemaphore.Destroy();
            base.Dispose();
        }
    }
}
