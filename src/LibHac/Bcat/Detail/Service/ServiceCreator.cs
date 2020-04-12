using LibHac.Bcat.Detail.Ipc;
using LibHac.Ncm;

namespace LibHac.Bcat.Detail.Service
{
    internal class ServiceCreator : IServiceCreator
    {
        private BcatServer Server { get; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private BcatServiceType ServiceType { get; }
        private AccessControl AccessControl { get; }

        public ServiceCreator(BcatServer server, BcatServiceType type, AccessControl accessControl)
        {
            Server = server;
            ServiceType = type;
            AccessControl = accessControl;
        }

        public Result CreateDeliveryCacheStorageServiceWithApplicationId(out IDeliveryCacheStorageService service,
            TitleId applicationId)
        {
            service = default;

            if (!AccessControl.HasFlag(AccessControl.Bit2))
                return ResultBcat.PermissionDenied.Log();

            return CreateDeliveryCacheStorageServiceImpl(out service, applicationId);
        }

        private Result CreateDeliveryCacheStorageServiceImpl(out IDeliveryCacheStorageService service,
            TitleId applicationId)
        {
            service = default;

            Result rc = Server.GetStorageManager().Open(applicationId.Value);
            if (rc.IsFailure()) return rc;

            // todo: Check if network account required

            service = new DeliveryCacheStorageService(Server, applicationId.Value, AccessControl);

            return Result.Success;
        }
    }
}
