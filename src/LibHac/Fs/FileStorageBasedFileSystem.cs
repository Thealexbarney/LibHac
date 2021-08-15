using LibHac.Common;
using LibHac.Fs.Fsa;

namespace LibHac.Fs
{
    public class FileStorageBasedFileSystem : FileStorage2
    {
        private ReferenceCountedDisposable<IFileSystem> _baseFileSystem;
        private UniqueRef<IFile> _baseFile;

        public FileStorageBasedFileSystem()
        {
            FileSize = SizeNotInitialized;
        }

        public Result Initialize(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, in Path path,
            OpenMode mode)
        {
            using var baseFile = new UniqueRef<IFile>();
            Result rc = baseFileSystem.Target.OpenFile(ref baseFile.Ref(), in path, mode);
            if (rc.IsFailure()) return rc;

            SetFile(baseFile.Get);
            _baseFileSystem = Shared.Move(ref baseFileSystem);
            _baseFile.Set(ref _baseFile.Ref());

            return Result.Success;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseFile.Destroy();
                _baseFileSystem?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
