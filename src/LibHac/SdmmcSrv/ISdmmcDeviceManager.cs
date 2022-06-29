using System;
using LibHac.Fs;
using LibHac.Os;
using LibHac.Sdmmc;

namespace LibHac.SdmmcSrv;

internal interface ISdmmcDeviceManager : IDisposable
{
    Result Lock(ref UniqueLockRef<SdkMutexType> outLock, SdmmcHandle handle);
    IStorage GetStorage();
    Port GetPort();
    void NotifyCloseStorageDevice(SdmmcHandle handle);
}