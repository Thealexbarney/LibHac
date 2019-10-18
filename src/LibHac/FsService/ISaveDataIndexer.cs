using LibHac.Fs;

namespace LibHac.FsService
{
    public interface ISaveDataIndexer
    {
        Result Commit();
        Result Add(out ulong saveDataId, ref SaveDataAttribute key);
        Result Get(out SaveDataIndexerValue value, ref SaveDataAttribute key);
        Result AddSystemSaveData(ref SaveDataAttribute key);
        bool IsFull();
        Result SetSpaceId(ulong saveDataId, SaveDataSpaceId spaceId);
        Result SetSize(ulong saveDataId, long size);
        Result SetState(ulong saveDataId, byte state);
        Result GetBySaveDataId(out SaveDataIndexerValue value, ulong saveDataId);
    }
}