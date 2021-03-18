using LibHac.Fs;

namespace LibHac.FsSrv.FsCreator
{
    public interface IBuiltInStorageCreator
    {
        Result Create(out ReferenceCountedDisposable<IStorage> storage, BisPartitionId partitionId);
        Result InvalidateCache();
    }
}
