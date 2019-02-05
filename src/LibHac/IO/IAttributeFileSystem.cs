using System.IO;

namespace LibHac.IO
{
    public interface IAttributeFileSystem : IFileSystem
    {
        FileAttributes GetFileAttributes(string path);
        void SetFileAttributes(string path, FileAttributes attributes);
        long GetFileSize(string path);
    }
}
