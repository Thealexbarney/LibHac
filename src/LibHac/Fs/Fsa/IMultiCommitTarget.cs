using LibHac.FsSrv.Sf;

namespace LibHac.Fs.Fsa
{
    public interface IMultiCommitTarget
    {
        ReferenceCountedDisposable<IFileSystemSf> GetMultiCommitTarget();
    }
}
