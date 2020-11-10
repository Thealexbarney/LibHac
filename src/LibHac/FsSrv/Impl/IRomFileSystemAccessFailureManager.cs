using System;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Ncm;

namespace LibHac.FsSrv.Impl
{
    public interface IRomFileSystemAccessFailureManager : IDisposable
    {
        Result OpenDataStorageCore(out ReferenceCountedDisposable<IStorage> storage, out Hash ncaHeaderDigest, ulong id,
            StorageId storageId);

        Result HandleResolubleAccessFailure(out bool wasDeferred, Result resultForNoFailureDetected);
        void IncrementRomFsRemountForDataCorruptionCount();
        void IncrementRomFsUnrecoverableDataCorruptionByRemountCount();
        void IncrementRomFsRecoveredByInvalidateCacheCount();
    }
}