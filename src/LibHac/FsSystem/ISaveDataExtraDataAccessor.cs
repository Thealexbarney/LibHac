using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public interface ISaveDataExtraDataAccessor : IDisposable
    {
        Result WriteExtraData(in SaveDataExtraData extraData);
        Result CommitExtraData(bool updateTimeStamp);
        Result ReadExtraData(out SaveDataExtraData extraData);
        void RegisterCacheObserver(ISaveDataExtraDataAccessorCacheObserver observer, SaveDataSpaceId spaceId, ulong saveDataId);
    }
}