using LibHac.Fs.Fsa;

namespace LibHac.FsSrv
{
    public interface IMultiCommitManager
    {
        Result Add(IFileSystem fileSystem);
        Result Commit();
    }
}
