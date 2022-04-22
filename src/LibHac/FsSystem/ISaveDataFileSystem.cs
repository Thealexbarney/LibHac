using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem;

// ReSharper disable once InconsistentNaming
public abstract class ISaveDataFileSystem : IFileSystem, ICacheableSaveDataFileSystem, ISaveDataExtraDataAccessor
{
    public abstract bool IsSaveDataFileSystemCacheEnabled();
    public abstract Result RollbackOnlyModified();

    public abstract Result WriteExtraData(in SaveDataExtraData extraData);
    public abstract Result CommitExtraData(bool updateTimeStamp);
    public abstract Result ReadExtraData(out SaveDataExtraData extraData);
    public abstract void RegisterCacheObserver(ISaveDataExtraDataAccessorObserver observer, SaveDataSpaceId spaceId, ulong saveDataId);
}

public interface ICacheableSaveDataFileSystem
{
    bool IsSaveDataFileSystemCacheEnabled();
    Result RollbackOnlyModified();
}