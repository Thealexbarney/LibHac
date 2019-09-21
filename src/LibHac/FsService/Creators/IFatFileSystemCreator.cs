using LibHac.FsSystem;

namespace LibHac.FsService.Creators
{
    public interface IFatFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, IStorage baseStorage);
    }
}