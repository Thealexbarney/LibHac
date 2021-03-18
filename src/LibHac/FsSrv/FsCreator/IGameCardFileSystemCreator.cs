using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public interface IGameCardFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, GameCardHandle handle, GameCardPartition partitionType);
        Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem, GameCardHandle handle, GameCardPartition partitionType);
    }
}