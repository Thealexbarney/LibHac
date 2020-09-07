using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Ncm;

namespace LibHac.FsSrv
{
    public interface IRomFileSystemAccessFailureManager
    {
        Result OpenDataStorageCore(out ReferenceCountedDisposable<IStorage> storage, out Hash ncaHeaderDigest, ulong id,
            StorageId storageId);

        Result HandleResolubleAccessFailure(out bool wasDeferred, in Result nonDeferredResult);
        void IncrementRomFsRemountForDataCorruptionCount();
        void IncrementRomFsUnrecoverableDataCorruptionByRemountCount();
        void IncrementRomFsRecoveredByInvalidateCacheCount();
    }
}
