using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Ncm;
using LibHac.Os;

namespace LibHac.Lr
{
    [NonCopyable]
    internal struct LrServiceGlobals : IDisposable
    {
        public SharedRef<ILocationResolverManager> LocationResolver;
        public SdkMutexType InitializationMutex;

        public void Initialize()
        {
            InitializationMutex.Initialize();
        }

        public void Dispose()
        {
            LocationResolver.Destroy();
        }
    }

    public static class LrService
    {
        public static void Initialize(this LrClient lr)
        {
            ref LrServiceGlobals globals = ref lr.Globals.LrService;
            Assert.SdkRequiresNotNull(globals.LocationResolver.Get);

            // The lock over getting the service object is a LibHac addition.
            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref lr.Globals.LrService.InitializationMutex);

            if (globals.LocationResolver.HasValue)
                return;

            using SharedRef<ILocationResolverManager> serviceObject = lr.GetLocationResolverManagerServiceObject();
            globals.LocationResolver.SetByMove(ref serviceObject.Ref());
        }

        public static Result OpenLocationResolver(this LrClient lr, out LocationResolver outResolver, StorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out outResolver);

            using var resolver = new SharedRef<ILocationResolver>();
            Result rc = lr.Globals.LrService.LocationResolver.Get.OpenLocationResolver(ref resolver.Ref(), storageId);
            if (rc.IsFailure()) return rc;

            outResolver = new LocationResolver(ref resolver.Ref());
            return Result.Success;
        }

        public static Result OpenRegisteredLocationResolver(this LrClient lr, out RegisteredLocationResolver outResolver)
        {
            UnsafeHelpers.SkipParamInit(out outResolver);

            using var resolver = new SharedRef<IRegisteredLocationResolver>();
            Result rc = lr.Globals.LrService.LocationResolver.Get.OpenRegisteredLocationResolver(ref resolver.Ref());
            if (rc.IsFailure()) return rc;

            outResolver = new RegisteredLocationResolver(ref resolver.Ref());
            return Result.Success;
        }

        public static Result OpenAddOnContentLocationResolver(this LrClient lr, out AddOnContentLocationResolver outResolver)
        {
            UnsafeHelpers.SkipParamInit(out outResolver);

            using var resolver = new SharedRef<IAddOnContentLocationResolver>();
            Result rc = lr.Globals.LrService.LocationResolver.Get.OpenAddOnContentLocationResolver(ref resolver.Ref());
            if (rc.IsFailure()) return rc;

            outResolver = new AddOnContentLocationResolver(ref resolver.Ref());
            return Result.Success;
        }

        public static Result RefreshLocationResolver(this LrClient lr, StorageId storageId)
        {
            Result rc = lr.Globals.LrService.LocationResolver.Get.RefreshLocationResolver(storageId);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        // Official lr puts this function along with memory allocation for
        // lr IPC objects into a separate file, LocationResolverManagerFactory.
        private static SharedRef<ILocationResolverManager> GetLocationResolverManagerServiceObject(this LrClient lr)
        {
            using var manager = new SharedRef<ILocationResolverManager>();
            Result rc = lr.Hos.Sm.GetService(ref manager.Ref(), "lr");

            if (rc.IsFailure())
            {
                throw new HorizonResultException(rc, "Failed to get lr service object.");
            }

            return SharedRef<ILocationResolverManager>.CreateMove(ref manager.Ref());
        }
    }
}
