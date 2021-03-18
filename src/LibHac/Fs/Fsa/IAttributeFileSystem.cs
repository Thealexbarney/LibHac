using LibHac.Common;

namespace LibHac.Fs.Fsa
{
    // ReSharper disable once InconsistentNaming
    public abstract class IAttributeFileSystem : IFileSystem
    {
        public Result CreateDirectory(U8Span path, NxFileAttributes archiveAttribute)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoCreateDirectory(path, archiveAttribute);
        }

        public Result GetFileAttributes(out NxFileAttributes attributes, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out attributes);

            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoGetFileAttributes(out attributes, path);
        }

        public Result SetFileAttributes(U8Span path, NxFileAttributes attributes)
        {
            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoSetFileAttributes(path, attributes);
        }

        public Result GetFileSize(out long fileSize, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out fileSize);

            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            return DoGetFileSize(out fileSize, path);
        }

        protected abstract Result DoCreateDirectory(U8Span path, NxFileAttributes archiveAttribute);
        protected abstract Result DoGetFileAttributes(out NxFileAttributes attributes, U8Span path);
        protected abstract Result DoSetFileAttributes(U8Span path, NxFileAttributes attributes);
        protected abstract Result DoGetFileSize(out long fileSize, U8Span path);
    }
}
