using LibHac.Fs;

namespace LibHac.FsSrv.Creators
{
    public interface IBuiltInStorageCreator
    {
        Result Create(out ReferenceCountedDisposable<IStorage> storage, BisPartitionId partitionId);
        Result InvalidateCache();
    }
}
