using LibHac.FsSystem;

namespace LibHac.FsService.Creators
{
    public interface ITargetManagerFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, bool openCaseSensitive);
        Result GetCaseSensitivePath(out bool isSuccess, ref string path);
    }
}