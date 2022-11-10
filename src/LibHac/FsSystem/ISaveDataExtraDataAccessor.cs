using System;
using LibHac.Fs;

namespace LibHac.FsSystem;

/// <summary>
/// Provides read/write access to a save data file system's extra data.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public interface ISaveDataExtraDataAccessor : IDisposable
{
    Result WriteExtraData(in SaveDataExtraData extraData);
    Result CommitExtraData(bool updateTimeStamp);
    Result ReadExtraData(out SaveDataExtraData extraData);
    void RegisterExtraDataAccessorObserver(ISaveDataExtraDataAccessorObserver observer, SaveDataSpaceId spaceId, ulong saveDataId);
}