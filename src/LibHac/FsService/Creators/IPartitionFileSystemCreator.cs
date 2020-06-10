using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsService.Creators
{
    public interface IPartitionFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, IStorage pFsStorage);
    }
}
