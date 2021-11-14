using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Ncm;

namespace LibHac.FsSrv.Impl;

public interface IRomFileSystemAccessFailureManager : IDisposable
{
    Result OpenDataStorageCore(ref SharedRef<IStorage> outStorage, out Hash ncaHeaderDigest, ulong id, StorageId storageId);
    Result HandleResolubleAccessFailure(out bool wasDeferred, Result resultForNoFailureDetected);
    void IncrementRomFsRemountForDataCorruptionCount();
    void IncrementRomFsUnrecoverableDataCorruptionByRemountCount();
    void IncrementRomFsRecoveredByInvalidateCacheCount();
}
