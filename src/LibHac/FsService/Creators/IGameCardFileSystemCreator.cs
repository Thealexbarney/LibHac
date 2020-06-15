using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsService.Creators
{
    public interface IGameCardFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, GameCardHandle handle, GameCardPartition partitionType);
    }
}