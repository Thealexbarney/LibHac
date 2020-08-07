using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Creators
{
    public interface IFatFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, IStorage baseStorage);
    }
}