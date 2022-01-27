using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSystem;

/// <summary>
/// Provides a mechanism for caching save data file systems.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public interface ISaveDataFileSystemCacheManager : IDisposable
{
    bool GetCache(ref SharedRef<SaveDataFileSystemHolder> outFileSystem, SaveDataSpaceId spaceId, ulong saveDataId);
    void Register(ref SharedRef<SaveDataFileSystemHolder> fileSystem);
    void Register(ref SharedRef<ApplicationTemporaryFileSystem> fileSystem);
    void Unregister(SaveDataSpaceId spaceId, ulong saveDataId);
}