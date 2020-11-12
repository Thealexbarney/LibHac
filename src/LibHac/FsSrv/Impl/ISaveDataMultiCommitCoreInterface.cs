using System;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Impl
{
    public interface ISaveDataMultiCommitCoreInterface : IDisposable
    {
        Result RecoverMultiCommit();
        Result IsProvisionallyCommittedSaveData(out bool isProvisionallyCommitted, in SaveDataInfo saveInfo);
        Result RecoverProvisionallyCommittedSaveData(in SaveDataInfo saveInfo, bool doRollback);
        Result OpenMultiCommitContext(out ReferenceCountedDisposable<IFileSystem> contextFileSystem);
    }
}
