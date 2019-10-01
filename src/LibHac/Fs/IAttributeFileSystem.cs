namespace LibHac.Fs
{
    public interface IAttributeFileSystem : IFileSystem
    {
        Result CreateDirectory(string path, NxFileAttributes archiveAttribute);
        Result GetFileAttributes(string path, out NxFileAttributes attributes);
        Result SetFileAttributes(string path, NxFileAttributes attributes);
        long GetFileSize(string path);
    }
}
