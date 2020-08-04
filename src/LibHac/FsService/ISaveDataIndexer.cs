using System;
using LibHac.Fs;

namespace LibHac.FsService
{
    public interface ISaveDataIndexer : IDisposable
    {
        Result Commit();
        Result Rollback();
        Result Reset();
        Result Publish(out ulong saveDataId, ref SaveDataAttribute key);
        Result Get(out SaveDataIndexerValue value, ref SaveDataAttribute key);
        Result PutStaticSaveDataIdIndex(ref SaveDataAttribute key);
        bool IsRemainedReservedOnly();
        Result Delete(ulong saveDataId);
        Result SetSpaceId(ulong saveDataId, SaveDataSpaceId spaceId);
        Result SetSize(ulong saveDataId, long size);
        Result SetState(ulong saveDataId, SaveDataState state);
        Result GetKey(out SaveDataAttribute key, ulong saveDataId);
        Result GetValue(out SaveDataIndexerValue value, ulong saveDataId);
        Result SetValue(ref SaveDataAttribute key, ref SaveDataIndexerValue value);
        int GetIndexCount();
        Result OpenSaveDataInfoReader(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader);
    }
}