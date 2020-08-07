using LibHac.Fs;

namespace LibHac.FsSrv.Creators
{
    public interface IBuiltInStorageCreator
    {
        Result Create(out IStorage storage, BisPartitionId partitionId);
    }
}
