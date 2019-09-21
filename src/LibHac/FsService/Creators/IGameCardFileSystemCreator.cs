using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public interface IGameCardFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, GameCardHandle handle, GameCardPartition partitionType);
    }
}