using LibHac.Fs;

namespace LibHac.FsSrv
{
    public interface ISaveDataIndexerManager
    {
        Result OpenAccessor(out SaveDataIndexerAccessor accessor, out bool neededInit, SaveDataSpaceId spaceId);
        void ResetTemporaryStorageIndexer(SaveDataSpaceId spaceId);
        void InvalidateSdCardIndexer(SaveDataSpaceId spaceId);
    }
}