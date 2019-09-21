using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public interface IRomFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, IStorage romFsStorage);
    }
}
