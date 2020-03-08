using LibHac.Common;

namespace LibHac.Fs
{
    public abstract class AttributeFileSystemBase : FileSystemBase, IAttributeFileSystem
    {
        protected abstract Result CreateDirectoryImpl(U8Span path, NxFileAttributes archiveAttribute);
        protected abstract Result GetFileAttributesImpl(out NxFileAttributes attributes, U8Span path);
        protected abstract Result SetFileAttributesImpl(U8Span path, NxFileAttributes attributes);
        protected abstract Result GetFileSizeImpl(out long fileSize, U8Span path);

        public Result CreateDirectory(U8Span path, NxFileAttributes archiveAttribute)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return CreateDirectoryImpl(path, archiveAttribute);
        }

        public Result GetFileAttributes(out NxFileAttributes attributes, U8Span path)
        {
            if (IsDisposed)
            {
                attributes = default;
                return ResultFs.PreconditionViolation.Log();
            }

            return GetFileAttributesImpl(out attributes, path);
        }

        public Result SetFileAttributes(U8Span path, NxFileAttributes attributes)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return SetFileAttributesImpl(path, attributes);
        }

        public Result GetFileSize(out long fileSize, U8Span path)
        {
            if (IsDisposed)
            {
                fileSize = default;
                return ResultFs.PreconditionViolation.Log();
            }

            return GetFileSizeImpl(out fileSize, path);
        }
    }
}
