using LibHac.Arp;
using LibHac.Bcat.Impl.Ipc;
using LibHac.Common;

namespace LibHac.Bcat.Impl.Service
{
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

        public Result CreateDeliveryCacheStorageService(out IDeliveryCacheStorageService service, ulong processId)
        {
            Result rc = Server.Hos.Arp.GetApplicationLaunchProperty(out ApplicationLaunchProperty launchProperty,
                processId);

            if (rc.IsFailure())
            {
                UnsafeHelpers.SkipParamInit(out service);
                return ResultBcat.NotFound.LogConverted(rc);
            }

            return CreateDeliveryCacheStorageServiceImpl(out service, launchProperty.ApplicationId);
        }

        public Result CreateDeliveryCacheStorageServiceWithApplicationId(out IDeliveryCacheStorageService service,
            ApplicationId applicationId)
        {
            if (!AccessControl.HasFlag(AccessControl.MountOthersDeliveryCacheStorage))
            {
                UnsafeHelpers.SkipParamInit(out service);
                return ResultBcat.PermissionDenied.Log();
            }

            return CreateDeliveryCacheStorageServiceImpl(out service, applicationId);
        }

        private Result CreateDeliveryCacheStorageServiceImpl(out IDeliveryCacheStorageService service,
            ApplicationId applicationId)
        {
            UnsafeHelpers.SkipParamInit(out service);

            Result rc = Server.GetStorageManager().Open(applicationId.Value);
            if (rc.IsFailure()) return rc;

            // todo: Check if network account required

            service = new DeliveryCacheStorageService(Server, applicationId.Value, AccessControl);

            return Result.Success;
        }
    }
}
