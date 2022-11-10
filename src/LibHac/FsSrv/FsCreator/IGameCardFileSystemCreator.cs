using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator;

public interface IGameCardFileSystemCreator
{
    Result Create(ref SharedRef<IFileSystem> outFileSystem, GameCardHandle handle, GameCardPartition partitionType);
}