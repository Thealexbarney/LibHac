using System;
using LibHac.Common;
using LibHac.Sm;

namespace LibHac.Bcat.Impl.Ipc;

internal class BcatServiceObject : IServiceObject
{
    private SharedRef<IServiceCreator> _serviceCreator;

    public BcatServiceObject(ref SharedRef<IServiceCreator> serviceCreator)
    {
        _serviceCreator = SharedRef<IServiceCreator>.CreateMove(ref serviceCreator);
    }

    public void Dispose()
    {
        _serviceCreator.Destroy();
    }

    public Result GetServiceObject(ref SharedRef<IDisposable> serviceObject)
    {
        serviceObject.SetByCopy(in _serviceCreator);
        return Result.Success;
    }
}