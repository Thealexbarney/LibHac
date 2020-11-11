using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Fsa
{
    public interface IMultiCommitTarget
    {
        ReferenceCountedDisposable<IFileSystemSf> GetMultiCommitTarget();
    }
}
