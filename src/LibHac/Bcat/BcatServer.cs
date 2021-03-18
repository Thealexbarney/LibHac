using System.Diagnostics;
using LibHac.Bcat.Impl.Ipc;
using LibHac.Bcat.Impl.Service;
using LibHac.Bcat.Impl.Service.Core;
using LibHac.Fs;

namespace LibHac.Bcat
{
    public class BcatServer
    {
        private const int ServiceTypeCount = 4;

        internal HorizonClient Hos { get; }
        private ServiceCreator[] ServiceCreators { get; } = new ServiceCreator[ServiceTypeCount];

        private readonly object _bcatServiceInitLocker = new object();
        private readonly object _storageManagerInitLocker = new object();

        private DeliveryCacheStorageManager StorageManager { get; set; }

        public BcatServer(HorizonClient horizonClient)
        {
            Hos = horizonClient;

            InitBcatService(BcatServiceType.BcatU, "bcat:u", AccessControl.MountOwnDeliveryCacheStorage);
            InitBcatService(BcatServiceType.BcatS, "bcat:s", AccessControl.MountOthersDeliveryCacheStorage);
            InitBcatService(BcatServiceType.BcatM, "bcat:m", AccessControl.MountOthersDeliveryCacheStorage | AccessControl.DeliveryTaskManagement);
            InitBcatService(BcatServiceType.BcatA, "bcat:a", AccessControl.All);
        }

        private void InitBcatService(BcatServiceType type, string name, AccessControl accessControl)
        {
            InitServiceCreator(type, name, accessControl);

            IServiceCreator service = GetServiceCreator(type);

            Result rc = Hos.Sm.RegisterService(new BcatServiceObject(service), name);
            if (rc.IsFailure())
            {
                throw new HorizonResultException(rc, "Abort");
            }
        }

        private void InitServiceCreator(BcatServiceType type, string name, AccessControl accessControl)
        {
            lock (_bcatServiceInitLocker)
            {
                Debug.Assert((uint)type < ServiceTypeCount);

                ServiceCreators[(int)type] = new ServiceCreator(this, name, accessControl);
            }
        }

        private IServiceCreator GetServiceCreator(BcatServiceType type)
        {
            Debug.Assert((uint)type < ServiceTypeCount);

            return ServiceCreators[(int)type];
        }

        internal DeliveryCacheStorageManager GetStorageManager()
        {
            return StorageManager ?? InitStorageManager();
        }

        internal FileSystemClient GetFsClient()
        {
            return Hos.Fs;
        }

        private DeliveryCacheStorageManager InitStorageManager()
        {
            lock (_storageManagerInitLocker)
            {
                if (StorageManager != null)
                {
                    return StorageManager;
                }

                StorageManager = new DeliveryCacheStorageManager(this);
                return StorageManager;
            }
        }
    }
}
