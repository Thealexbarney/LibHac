using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public interface ITargetManagerFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, bool someBool);
        Result GetCaseSensitivePath(out bool isSuccess, ref string path);
    }
}