using LibHac.Fs;

namespace LibHac.FsService
{
    public interface IMultiCommitManager
    {
        Result Add(IFileSystem fileSystem);
        Result Commit();
    }
}
