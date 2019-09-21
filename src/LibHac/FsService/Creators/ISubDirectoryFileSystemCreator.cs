using LibHac.FsSystem;

namespace LibHac.FsService.Creators
{
    public interface ISubDirectoryFileSystemCreator
    {
        Result Create(out IFileSystem subDirFileSystem, IFileSystem baseFileSystem, string path);
    }
}