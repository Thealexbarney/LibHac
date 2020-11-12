using LibHac.Fs;

namespace LibHac.FsSrv
{
    public interface ISaveDataIndexerManager
    {
        Result OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, out bool neededInit, SaveDataSpaceId spaceId);
        void ResetIndexer(SaveDataSpaceId spaceId);
        void InvalidateIndexer(SaveDataSpaceId spaceId);
    }
}