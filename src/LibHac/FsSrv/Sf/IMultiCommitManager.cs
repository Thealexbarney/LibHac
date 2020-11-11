using System;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.FsSrv.Sf
{
    public interface IMultiCommitManager : IDisposable
    {
        Result Add(ReferenceCountedDisposable<IFileSystemSf> fileSystem);
        Result Commit();
    }
}
