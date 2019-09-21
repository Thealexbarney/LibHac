using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    public interface IBuiltInStorageCreator
    {
        Result Create(out IStorage storage, BisPartitionId partitionId);
    }
}
