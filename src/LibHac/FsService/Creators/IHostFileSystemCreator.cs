using LibHac.Fs.Fsa;

namespace LibHac.FsService.Creators
{
    public interface IHostFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, bool someBool);
        Result Create(out IFileSystem fileSystem, string path, bool openCaseSensitive);
    }
}