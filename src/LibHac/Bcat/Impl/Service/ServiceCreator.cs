using LibHac.Arp;
using LibHac.Bcat.Impl.Ipc;
using LibHac.Common;

namespace LibHac.Bcat.Impl.Service;

// Todo: Update BCAT service object management
internal class ServiceCreator : IServiceCreator
{
    private BcatServer Server { get; }

    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    private string ServiceName { get; }
    private AccessControl AccessControl { get; }

    public ServiceCreator(BcatServer server, string serviceName, AccessControl accessControl)
    {
        Server = server;
        ServiceName = serviceName;
        AccessControl = accessControl;
    }

    public void Dispose() { }

    public Result CreateDeliveryCacheStorageService(ref SharedRef<IDeliveryCacheStorageService> outService,
        ulong processId)
    {
        Result res = Server.Hos.Arp.GetApplicationLaunchProperty(out ApplicationLaunchProperty launchProperty,
            processId);

        if (res.IsFailure())
            return ResultBcat.NotFound.LogConverted(res);

        return CreateDeliveryCacheStorageServiceImpl(ref outService, launchProperty.ApplicationId);
    }

    public Result CreateDeliveryCacheStorageServiceWithApplicationId(
        ref SharedRef<IDeliveryCacheStorageService> outService, ApplicationId applicationId)
    {
        if (!AccessControl.HasFlag(AccessControl.MountOthersDeliveryCacheStorage))
            return ResultBcat.PermissionDenied.Log();

        return CreateDeliveryCacheStorageServiceImpl(ref outService, applicationId);
    }

    private Result CreateDeliveryCacheStorageServiceImpl(ref SharedRef<IDeliveryCacheStorageService> outService,
        ApplicationId applicationId)
    {
        Result res = Server.GetStorageManager().Open(applicationId.Value);
        if (res.IsFailure()) return res.Miss();

        // todo: Check if network account required

        outService.Reset(new DeliveryCacheStorageService(Server, applicationId.Value, AccessControl));

        return Result.Success;
    }
}
