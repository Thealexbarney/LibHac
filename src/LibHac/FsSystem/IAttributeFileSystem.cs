namespace LibHac.FsSystem
{
    public interface IAttributeFileSystem : IFileSystem
    {
        NxFileAttributes GetFileAttributes(string path);
        void SetFileAttributes(string path, NxFileAttributes attributes);
        long GetFileSize(string path);
    }
}
