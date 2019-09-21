using LibHac.FsSystem;

namespace LibHac.FsService.Creators
{
    public interface IPartitionFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, IStorage pFsStorage);
    }
}
