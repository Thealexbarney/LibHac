using LibHac.Fs;

namespace LibHac.SdmmcSrv
{
    internal interface ISdmmcDeviceManager
    {
        Result Lock(out object locker, uint handle);
        IStorage GetStorage();
        SdmmcPort GetPortId();
        Result NotifyCloseStorageDevice(uint handle);
    }
}
