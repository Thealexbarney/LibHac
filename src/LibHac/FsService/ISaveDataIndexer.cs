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
        Result Delete(ulong saveDataId);
        Result SetSpaceId(ulong saveDataId, SaveDataSpaceId spaceId);
        Result SetSize(ulong saveDataId, long size);
        Result SetState(ulong saveDataId, SaveDataState state);
        Result GetKey(out SaveDataAttribute key, ulong saveDataId);
        Result GetBySaveDataId(out SaveDataIndexerValue value, ulong saveDataId);
        Result OpenSaveDataInfoReader(out ISaveDataInfoReader infoReader);
    }
}