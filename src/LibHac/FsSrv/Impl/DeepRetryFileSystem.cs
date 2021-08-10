using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl
{
    public class DeepRetryFileSystem : ForwardingFileSystem
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private ReferenceCountedDisposable<DeepRetryFileSystem>.WeakReference SelfReference { get; set; }
        private ReferenceCountedDisposable<IRomFileSystemAccessFailureManager> AccessFailureManager { get; set; }

        protected DeepRetryFileSystem(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem,
            ref ReferenceCountedDisposable<IRomFileSystemAccessFailureManager> accessFailureManager) : base(
            ref baseFileSystem)
        {
            AccessFailureManager = Shared.Move(ref accessFailureManager);
        }

        public static ReferenceCountedDisposable<IFileSystem> CreateShared(
            ref ReferenceCountedDisposable<IFileSystem> fileSystem,
            ref ReferenceCountedDisposable<IRomFileSystemAccessFailureManager> accessFailureManager)
        {
            ReferenceCountedDisposable<DeepRetryFileSystem> sharedRetryFileSystem = null;
            try
            {
                var retryFileSystem = new DeepRetryFileSystem(ref fileSystem, ref accessFailureManager);
                sharedRetryFileSystem = new ReferenceCountedDisposable<DeepRetryFileSystem>(retryFileSystem);

                retryFileSystem.SelfReference =
                    new ReferenceCountedDisposable<DeepRetryFileSystem>.WeakReference(sharedRetryFileSystem);

                return sharedRetryFileSystem.AddReference<IFileSystem>();
            }
            finally
            {
                sharedRetryFileSystem?.Dispose();
            }
        }

        public override void Dispose()
        {
            AccessFailureManager?.Dispose();
            base.Dispose();
        }

        // ReSharper disable once RedundantOverriddenMember
        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
        {
            // Todo: Implement
            return base.DoOpenFile(ref outFile, path, mode);
        }
    }
}
