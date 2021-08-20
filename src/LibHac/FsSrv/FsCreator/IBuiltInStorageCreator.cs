using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSrv.FsCreator
{
    public interface IBuiltInStorageCreator
    {
        Result Create(ref SharedRef<IStorage> outStorage, BisPartitionId partitionId);
        Result InvalidateCache();
    }
}
