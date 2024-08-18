using System;
using LibHac.Common;

namespace LibHac.FsSrv.Impl;

public class SystemDataUpdateEventManager : IDisposable
{
    public SystemDataUpdateEventManager()
    {
        // Todo: Implement
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result CreateNotifier(ref UniqueRef<SystemDataUpdateEventNotifier> outNotifier)
    {
        throw new NotImplementedException();
    }

    public Result NotifySystemDataUpdateEvent()
    {
        throw new NotImplementedException();
    }

    public void DeleteNotifier(SystemDataUpdateEventNotifier notifier)
    {
        throw new NotImplementedException();
    }
}