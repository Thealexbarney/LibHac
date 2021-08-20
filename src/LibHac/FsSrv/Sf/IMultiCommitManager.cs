using System;
using LibHac.Common;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.FsSrv.Sf
{
    public interface IMultiCommitManager : IDisposable
    {
        Result Add(ref SharedRef<IFileSystemSf> fileSystem);
        Result Commit();
    }
}
