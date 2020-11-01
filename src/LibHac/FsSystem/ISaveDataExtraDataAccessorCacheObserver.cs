using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public interface ISaveDataExtraDataAccessorCacheObserver : IDisposable
    {
        void Unregister(SaveDataSpaceId spaceId, ulong saveDataId);
    }
}