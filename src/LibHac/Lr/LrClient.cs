using System;
using LibHac.Common;
using LibHac.Ncm;

namespace LibHac.Lr
{
    public class LrClient : IDisposable
    {
        private HorizonClient Hos { get; }

        private ILocationResolverManager LrManager { get; set; }
        private readonly object _lrInitLocker = new object();

        public LrClient(HorizonClient horizonClient)
        {
            Hos = horizonClient;
        }

        public Result OpenLocationResolver(out LocationResolver resolver, StorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out resolver);
            EnsureInitialized();

            Result rc = LrManager.OpenLocationResolver(out ReferenceCountedDisposable<ILocationResolver> baseResolver,
                storageId);
            if (rc.IsFailure()) return rc;

            using (baseResolver)
            {
                resolver = new LocationResolver(baseResolver);
                return Result.Success;
            }
        }

        public Result OpenRegisteredLocationResolver(out RegisteredLocationResolver resolver)
        {
            UnsafeHelpers.SkipParamInit(out resolver);
            EnsureInitialized();

            Result rc = LrManager.OpenRegisteredLocationResolver(
                out ReferenceCountedDisposable<IRegisteredLocationResolver> baseResolver);
            if (rc.IsFailure()) return rc;

            using (baseResolver)
            {
                resolver = new RegisteredLocationResolver(baseResolver);
                return Result.Success;
            }
        }

        public Result OpenAddOnContentLocationResolver(out AddOnContentLocationResolver resolver)
        {
            UnsafeHelpers.SkipParamInit(out resolver);
            EnsureInitialized();

            Result rc = LrManager.OpenAddOnContentLocationResolver(
                out ReferenceCountedDisposable<IAddOnContentLocationResolver> baseResolver);
            if (rc.IsFailure()) return rc;

            using (baseResolver)
            {
                resolver = new AddOnContentLocationResolver(baseResolver);
                return Result.Success;
            }
        }

        public Result RefreshLocationResolver(StorageId storageId)
        {
            EnsureInitialized();

            Result rc = LrManager.RefreshLocationResolver(storageId);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        private void EnsureInitialized()
        {
            if (LrManager != null)
                return;

            lock (_lrInitLocker)
            {
                if (LrManager != null)
                    return;

                Result rc = Hos.Sm.GetService(out ILocationResolverManager manager, "lr");

                if (rc.IsFailure())
                {
                    throw new HorizonResultException(rc, "Failed to initialize lr client.");
                }

                LrManager = manager;
            }
        }

        public void Dispose()
        {
            LrManager?.Dispose();
        }
    }
}
