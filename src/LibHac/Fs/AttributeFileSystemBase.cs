namespace LibHac.Fs
{
    public abstract class AttributeFileSystemBase : FileSystemBase, IAttributeFileSystem
    {
        protected abstract Result CreateDirectoryImpl(string path, NxFileAttributes archiveAttribute);
        protected abstract Result GetFileAttributesImpl(string path, out NxFileAttributes attributes);
        protected abstract Result SetFileAttributesImpl(string path, NxFileAttributes attributes);
        protected abstract Result GetFileSizeImpl(out long fileSize, string path);

        public Result CreateDirectory(string path, NxFileAttributes archiveAttribute)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return CreateDirectoryImpl(path, archiveAttribute);
        }

        public Result GetFileAttributes(string path, out NxFileAttributes attributes)
        {
            if (IsDisposed)
            {
                attributes = default;
                return ResultFs.PreconditionViolation.Log();
            }

            return GetFileAttributesImpl(path, out attributes);
        }

        public Result SetFileAttributes(string path, NxFileAttributes attributes)
        {
            if (IsDisposed) return ResultFs.PreconditionViolation.Log();

            return SetFileAttributesImpl(path, attributes);
        }

        public Result GetFileSize(out long fileSize, string path)
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
