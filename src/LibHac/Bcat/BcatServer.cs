using System.Diagnostics;
using LibHac.Bcat.Detail.Ipc;
using LibHac.Bcat.Detail.Service;
using LibHac.Bcat.Detail.Service.Core;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.Bcat
{
    public class BcatServer
    {
        private const int ServiceTypeCount = 4;

        private Horizon ServiceManager { get; }
        private ServiceCreator[] ServiceCreators { get; } = new ServiceCreator[ServiceTypeCount];

        private readonly object _storageManagerInitLocker = new object();
        private readonly object _fsInitLocker = new object();

        private DeliveryCacheStorageManager StorageManager { get; set; }
        private FileSystemClient FsClient { get; set; }

        public BcatServer(Horizon horizon)
        {
            ServiceManager = horizon;

            InitServiceCreator(BcatServiceType.BcatU, AccessControl.Bit1);
            InitServiceCreator(BcatServiceType.BcatS, AccessControl.Bit2);
            InitServiceCreator(BcatServiceType.BcatM, AccessControl.Bit2 | AccessControl.Bit3);
            InitServiceCreator(BcatServiceType.BcatA, AccessControl.All);
        }

        public Result GetServiceCreator(out IServiceCreator serviceCreator, BcatServiceType type)
        {
            if ((uint)type < ServiceTypeCount)
            {
                serviceCreator = default;
                return ResultLibHac.ArgumentOutOfRange.Log();
            }

            serviceCreator = ServiceCreators[(int)type];
            return Result.Success;
        }

        private void InitServiceCreator(BcatServiceType type, AccessControl accessControl)
        {
            Debug.Assert((uint)type < ServiceTypeCount);

            ServiceCreators[(int)type] = new ServiceCreator(this, type, accessControl);
        }

        internal DeliveryCacheStorageManager GetStorageManager()
        {
            return StorageManager ?? InitStorageManager();
        }

        internal FileSystemClient GetFsClient()
        {
            return FsClient ?? InitFsClient();
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

        private FileSystemClient InitFsClient()
        {
            lock (_fsInitLocker)
            {
                if (FsClient != null)
                {
                    return FsClient;
                }

                Result rc = ServiceManager.OpenFileSystemClient(out FileSystemClient fsClient);

                if (!rc.IsSuccess())
                    throw new HorizonResultException(rc, "Abort");

                return fsClient;
            }
        }
    }
}
