using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Creators
{
    public interface IRomFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, IStorage romFsStorage);
    }
}
