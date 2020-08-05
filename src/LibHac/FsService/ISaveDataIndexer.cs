using System;
using LibHac.Fs;

namespace LibHac.FsService
{
    public interface ISaveDataIndexer : IDisposable
    {
        Result Commit();
        Result Rollback();
        Result Reset();
        Result Publish(out ulong saveDataId, in SaveDataAttribute key);
        Result Get(out SaveDataIndexerValue value, in SaveDataAttribute key);
        Result PutStaticSaveDataIdIndex(in SaveDataAttribute key);
        bool IsRemainedReservedOnly();
        Result Delete(ulong saveDataId);
        Result SetSpaceId(ulong saveDataId, SaveDataSpaceId spaceId);
        Result SetSize(ulong saveDataId, long size);
        Result SetState(ulong saveDataId, SaveDataState state);
        Result GetKey(out SaveDataAttribute key, ulong saveDataId);
        Result GetValue(out SaveDataIndexerValue value, ulong saveDataId);
        Result SetValue(in SaveDataAttribute key, in SaveDataIndexerValue value);
        int GetIndexCount();
        Result OpenSaveDataInfoReader(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader);
    }
}