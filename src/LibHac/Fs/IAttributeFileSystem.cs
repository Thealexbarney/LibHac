using LibHac.Common;

namespace LibHac.Fs
{
    public interface IAttributeFileSystem : IFileSystem
    {
        Result CreateDirectory(U8Span path, NxFileAttributes archiveAttribute);
        Result GetFileAttributes(out NxFileAttributes attributes, U8Span path);
        Result SetFileAttributes(U8Span path, NxFileAttributes attributes);
        Result GetFileSize(out long fileSize, U8Span path);
    }
}
