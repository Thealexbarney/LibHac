using LibHac.Fs.Fsa;

namespace LibHac.FsService.Creators
{
    public interface ISdFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem);
        Result Format(bool closeOpenEntries);
    }
}
