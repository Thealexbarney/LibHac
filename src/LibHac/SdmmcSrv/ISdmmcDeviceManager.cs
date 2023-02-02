using System;
using LibHac.Fs;
using LibHac.Os;
using LibHac.Sdmmc;

namespace LibHac.SdmmcSrv;

/// <summary>
/// Manages locking and getting the storage from sdmmc devices.
/// </summary>
/// <remarks>Based on nnSdk 15.3.0 (FS 15.0.0)</remarks>
internal interface ISdmmcDeviceManager : IDisposable
{
    Result Lock(ref UniqueLockRef<SdkMutexType> outLock, SdmmcHandle handle);
    IStorage GetStorage();
    Port GetPort();
    void NotifyCloseStorageDevice(SdmmcHandle handle);
}