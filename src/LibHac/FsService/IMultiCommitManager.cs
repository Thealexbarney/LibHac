using LibHac.Fs.Fsa;

namespace LibHac.FsService
{
    public interface IMultiCommitManager
    {
        Result Add(IFileSystem fileSystem);
        Result Commit();
    }
}
