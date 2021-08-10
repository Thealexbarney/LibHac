using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSrv
{
    public interface ISaveDataIndexerManager
    {
        Result OpenSaveDataIndexerAccessor(ref UniqueRef<SaveDataIndexerAccessor> outAccessor, out bool neededInit, SaveDataSpaceId spaceId);
        void ResetIndexer(SaveDataSpaceId spaceId);
        void InvalidateIndexer(SaveDataSpaceId spaceId);
    }
}