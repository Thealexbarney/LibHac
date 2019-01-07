using System.IO;

namespace LibHac.IO
{
    public interface IAttributeFileSystem : IFileSystem
    {
        FileAttributes GetFileAttributes(string path);
        long GetFileSize(string path);
    }
}
