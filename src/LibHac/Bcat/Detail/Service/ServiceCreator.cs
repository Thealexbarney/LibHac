using System;
using LibHac.Bcat.Detail.Ipc;
using LibHac.Ncm;

namespace LibHac.Bcat.Detail.Service
{
    internal class ServiceCreator : IServiceCreator
    {
        private BcatServer Parent { get; }
        private BcatServiceType ServiceType { get; }
        private AccessControl AccessControl { get; }

        public ServiceCreator(BcatServer parentServer, BcatServiceType type, AccessControl accessControl)
        {
            Parent = parentServer;
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
            throw new NotImplementedException();
        }
    }
}
