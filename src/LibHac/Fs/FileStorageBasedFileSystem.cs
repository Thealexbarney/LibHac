using LibHac.Common;

namespace LibHac.Fs
{
    public class FileStorageBasedFileSystem : FileStorage2
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        // FS keeps a shared pointer to the base filesystem
        private IFileSystem BaseFileSystem { get; set; }
        private IFile BaseFile { get; set; }

        private FileStorageBasedFileSystem()
        {
            FileSize = InvalidSize;
        }

        public static Result CreateNew(out FileStorageBasedFileSystem created, IFileSystem baseFileSystem, U8Span path,
            OpenMode mode)
        {
            var obj = new FileStorageBasedFileSystem();
            Result rc = obj.Initialize(baseFileSystem, path, mode);

            if (rc.IsSuccess())
            {
                created = obj;
                return Result.Success;
            }

            obj.Dispose();
            created = default;
            return rc;
        }

        private Result Initialize(IFileSystem baseFileSystem, U8Span path, OpenMode mode)
        {
            Result rc = baseFileSystem.OpenFile(out IFile file, path, mode);
            if (rc.IsFailure()) return rc;

            SetFile(file);
            BaseFile = file;
            BaseFileSystem = baseFileSystem;

            return Result.Success;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BaseFile?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
