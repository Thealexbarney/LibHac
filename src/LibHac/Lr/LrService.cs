using LibHac.Common;
using LibHac.Diag;
using LibHac.Ncm;
using LibHac.Os;

namespace LibHac.Lr
{
    internal struct LrServiceGlobals
    {
        public ILocationResolverManager LocationResolver;
        public SdkMutex InitializationMutex;

        public void Initialize()
        {
            LocationResolver = null;
            InitializationMutex.Initialize();
        }

        public void Dispose()
        {
            if (LocationResolver is not null)
            {
                LocationResolver.Dispose();
                LocationResolver = null;
            }
        }
    }

    public static class LrService
    {
        public static void Initialize(this LrClient lr)
        {
            ref LrServiceGlobals globals = ref lr.Globals.LrService;
            Assert.SdkRequiresNotNull(globals.LocationResolver);

            // The lock over getting the service object is a LibHac addition.
            using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref lr.Globals.LrService.InitializationMutex);

            if (globals.LocationResolver is not null)
                return;

            ILocationResolverManager serviceObject = lr.GetLocationResolverManagerServiceObject();
            globals.LocationResolver = serviceObject;
        }

        public static Result OpenLocationResolver(this LrClient lr, out LocationResolver resolver, StorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out resolver);

            Result rc = lr.Globals.LrService.LocationResolver.OpenLocationResolver(
                out ReferenceCountedDisposable<ILocationResolver> baseResolver, storageId);
            if (rc.IsFailure()) return rc;

            resolver = new LocationResolver(ref baseResolver);
            return Result.Success;
        }

        public static Result OpenRegisteredLocationResolver(this LrClient lr, out RegisteredLocationResolver resolver)
        {
            UnsafeHelpers.SkipParamInit(out resolver);

            Result rc = lr.Globals.LrService.LocationResolver.OpenRegisteredLocationResolver(
                out ReferenceCountedDisposable<IRegisteredLocationResolver> baseResolver);
            if (rc.IsFailure()) return rc;

            resolver = new RegisteredLocationResolver(ref baseResolver);
            return Result.Success;
        }

        public static Result OpenAddOnContentLocationResolver(this LrClient lr, out AddOnContentLocationResolver resolver)
        {
            UnsafeHelpers.SkipParamInit(out resolver);

            Result rc = lr.Globals.LrService.LocationResolver.OpenAddOnContentLocationResolver(
                out ReferenceCountedDisposable<IAddOnContentLocationResolver> baseResolver);
            if (rc.IsFailure()) return rc;

            resolver = new AddOnContentLocationResolver(ref baseResolver);
            return Result.Success;
        }

        public static Result RefreshLocationResolver(this LrClient lr, StorageId storageId)
        {
            Result rc = lr.Globals.LrService.LocationResolver.RefreshLocationResolver(storageId);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }
        
        // Official lr puts this function along with memory allocation for
        // lr IPC objects into a separate file, LocationResolverManagerFactory.
        private static ILocationResolverManager GetLocationResolverManagerServiceObject(this LrClient lr)
        {
            Result rc = lr.Hos.Sm.GetService(out ILocationResolverManager manager, "lr");

            if (rc.IsFailure())
            {
                throw new HorizonResultException(rc, "Failed to get lr service object.");
            }

            return manager;
        }
    }
}
