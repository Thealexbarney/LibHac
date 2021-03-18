using System;

namespace LibHac.Bcat.Impl.Service
{
    [Flags]
    internal enum AccessControl
    {
        None = 0,
        MountOwnDeliveryCacheStorage = 1 << 1,
        MountOthersDeliveryCacheStorage = 1 << 2,
        DeliveryTaskManagement = 1 << 3,
        Debug = 1 << 4,
        All = ~0
    }
}
