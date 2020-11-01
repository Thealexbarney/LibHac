using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl
{
    public class AsynchronousAccessFileSystem : ForwardingFileSystem
    {
        public AsynchronousAccessFileSystem(ReferenceCountedDisposable<IFileSystem> baseFileSystem) : base(
            baseFileSystem)
        { }

        protected AsynchronousAccessFileSystem(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem) : base(
            ref baseFileSystem)
        { }

        public static ReferenceCountedDisposable<IFileSystem> CreateShared(
            ReferenceCountedDisposable<IFileSystem> baseFileSystem)
        {
            return new ReferenceCountedDisposable<IFileSystem>(new AsynchronousAccessFileSystem(baseFileSystem));
        }

        public static ReferenceCountedDisposable<IFileSystem> CreateShared(
            ref ReferenceCountedDisposable<IFileSystem> fileSystem)
        {
            return new ReferenceCountedDisposable<IFileSystem>(new AsynchronousAccessFileSystem(ref fileSystem));
        }

        // ReSharper disable once RedundantOverriddenMember
        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            // Todo: Implement
            return base.DoOpenFile(out file, path, mode);
        }
    }
}
