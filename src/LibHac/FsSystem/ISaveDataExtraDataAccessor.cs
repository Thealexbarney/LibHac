using System;
using LibHac.Fs;

namespace LibHac.FsSystem;

/// <summary>
/// Provides read/write access to a save data file system's extra data.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public interface ISaveDataExtraDataAccessor : IDisposable
{
    Result WriteExtraData(in SaveDataExtraData extraData);
    Result CommitExtraData(bool updateTimeStamp);
    Result ReadExtraData(out SaveDataExtraData extraData);
    void RegisterCacheObserver(ISaveDataExtraDataAccessorObserver observer, SaveDataSpaceId spaceId, ulong saveDataId);
}