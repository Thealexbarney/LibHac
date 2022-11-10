using System;
using LibHac.Fs;
using LibHac.FsSrv.Impl;

namespace LibHac.FsSystem;

/// <summary>
/// Used when adding an <see cref="ISaveDataExtraDataAccessor"/> to the
/// <see cref="SaveDataExtraDataAccessorCacheManager"/>. When an extra data accessor is disposed, the accessor will
/// use this interface to notify the cache manager that it should be removed from the extra data cache.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public interface ISaveDataExtraDataAccessorObserver : IDisposable
{
    void Unregister(SaveDataSpaceId spaceId, ulong saveDataId);
}