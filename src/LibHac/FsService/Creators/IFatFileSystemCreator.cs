using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsService.Creators
{
    public interface IFatFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, IStorage baseStorage);
    }
}