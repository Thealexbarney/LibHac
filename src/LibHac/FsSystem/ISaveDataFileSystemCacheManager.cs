using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSystem;

public interface ISaveDataFileSystemCacheManager : IDisposable
{
    bool GetCache(ref SharedRef<SaveDataFileSystemHolder> outFileSystem, SaveDataSpaceId spaceId, ulong saveDataId);
    void Register(ref SharedRef<SaveDataFileSystemHolder> fileSystem);
    void Register(ref SharedRef<ApplicationTemporaryFileSystem> fileSystem);
    void Unregister(SaveDataSpaceId spaceId, ulong saveDataId);
}
