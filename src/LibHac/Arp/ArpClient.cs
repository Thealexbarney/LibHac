using System;
using LibHac.Arp.Impl;
using LibHac.Common;
using LibHac.Ns;

namespace LibHac.Arp;

public class ArpClient : IDisposable
{
    private HorizonClient _hosClient;
    private SharedRef<IReader> _reader;

    private readonly object _readerInitLocker = new object();

    internal ArpClient(HorizonClient horizonClient)
    {
        _hosClient = horizonClient;
    }

    public void Dispose()
    {
        _reader.Destroy();
    }

    public Result GetApplicationLaunchProperty(out ApplicationLaunchProperty launchProperty, ulong processId)
    {
        EnsureReaderInitialized();

        return _reader.Get.GetApplicationLaunchProperty(out launchProperty, processId);
    }

    public Result GetApplicationLaunchProperty(out ApplicationLaunchProperty launchProperty, ApplicationId applicationId)
    {
        EnsureReaderInitialized();

        return _reader.Get.GetApplicationLaunchPropertyWithApplicationId(out launchProperty, applicationId);
    }

    public Result GetApplicationControlProperty(out ApplicationControlProperty controlProperty, ulong processId)
    {
        EnsureReaderInitialized();

        return _reader.Get.GetApplicationControlProperty(out controlProperty, processId);
    }

    public Result GetApplicationControlProperty(out ApplicationControlProperty controlProperty, ApplicationId applicationId)
    {
        EnsureReaderInitialized();

        return _reader.Get.GetApplicationControlPropertyWithApplicationId(out controlProperty, applicationId);
    }

    private void EnsureReaderInitialized()
    {
        if (_reader.HasValue)
            return;

        lock (_readerInitLocker)
        {
            if (_reader.HasValue)
                return;

            using var reader = new SharedRef<IReader>();
            Result res = _hosClient.Sm.GetService(ref reader.Ref, "arp:r");

            if (res.IsFailure())
            {
                throw new HorizonResultException(res, "Failed to initialize arp reader.");
            }

            _reader.SetByMove(ref reader.Ref);
        }
    }
}