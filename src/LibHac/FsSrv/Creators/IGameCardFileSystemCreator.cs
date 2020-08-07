using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Creators
{
    public interface IGameCardFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, GameCardHandle handle, GameCardPartition partitionType);
    }
}