using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Creators
{
    public interface IPartitionFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, IStorage pFsStorage);
    }
}
