namespace LibHac.Fs
{
    public interface IAttributeFileSystem : IFileSystem
    {
        Result CreateDirectory(string path, NxFileAttributes archiveAttribute);
        Result GetFileAttributes(out NxFileAttributes attributes, string path);
        Result SetFileAttributes(string path, NxFileAttributes attributes);
        Result GetFileSize(out long fileSize, string path);
    }
}
