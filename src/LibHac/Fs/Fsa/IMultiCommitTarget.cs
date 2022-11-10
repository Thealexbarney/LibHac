using LibHac.Common;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Fsa;

public interface IMultiCommitTarget
{
    SharedRef<IFileSystemSf> GetMultiCommitTarget();
}