using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Creators
{
    public interface ISdFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem);
        Result Format(bool closeOpenEntries);
    }
}
