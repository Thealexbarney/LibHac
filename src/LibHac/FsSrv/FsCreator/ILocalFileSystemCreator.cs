using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public interface ILocalFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, bool someBool);
        Result Create(out IFileSystem fileSystem, string path, bool openCaseSensitive);
    }
}