using LibHac.Common;
using LibHac.Fs.Fsa;

namespace LibHac.Fs
{
    public class FileStorageBasedFileSystem : FileStorage2
    {
        private ReferenceCountedDisposable<IFileSystem> BaseFileSystem { get; set; }
        private IFile BaseFile { get; set; }

        public FileStorageBasedFileSystem()
        {
            FileSize = SizeNotInitialized;
        }

        public Result Initialize(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem, U8Span path, OpenMode mode)
        {
            Result rc = baseFileSystem.Target.OpenFile(out IFile file, path, mode);
            if (rc.IsFailure()) return rc;

            SetFile(file);
            BaseFile = file;
            BaseFileSystem = Shared.Move(ref baseFileSystem);

            return Result.Success;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BaseFile?.Dispose();
                BaseFileSystem?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
