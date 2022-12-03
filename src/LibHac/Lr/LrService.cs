using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Ncm;
using LibHac.Os;

namespace LibHac.Lr;

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
        globals.LocationResolver.SetByMove(ref serviceObject.Ref);
    }

    public static Result OpenLocationResolver(this LrClient lr, out LocationResolver outResolver, StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out outResolver);

        using var resolver = new SharedRef<ILocationResolver>();
        Result res = lr.Globals.LrService.LocationResolver.Get.OpenLocationResolver(ref resolver.Ref, storageId);
        if (res.IsFailure()) return res.Miss();

        outResolver = new LocationResolver(ref resolver.Ref);
        return Result.Success;
    }

    public static Result OpenRegisteredLocationResolver(this LrClient lr, out RegisteredLocationResolver outResolver)
    {
        UnsafeHelpers.SkipParamInit(out outResolver);

        using var resolver = new SharedRef<IRegisteredLocationResolver>();
        Result res = lr.Globals.LrService.LocationResolver.Get.OpenRegisteredLocationResolver(ref resolver.Ref);
        if (res.IsFailure()) return res.Miss();

        outResolver = new RegisteredLocationResolver(ref resolver.Ref);
        return Result.Success;
    }

    public static Result OpenAddOnContentLocationResolver(this LrClient lr, out AddOnContentLocationResolver outResolver)
    {
        UnsafeHelpers.SkipParamInit(out outResolver);

        using var resolver = new SharedRef<IAddOnContentLocationResolver>();
        Result res = lr.Globals.LrService.LocationResolver.Get.OpenAddOnContentLocationResolver(ref resolver.Ref);
        if (res.IsFailure()) return res.Miss();

        outResolver = new AddOnContentLocationResolver(ref resolver.Ref);
        return Result.Success;
    }

    public static Result RefreshLocationResolver(this LrClient lr, StorageId storageId)
    {
        Result res = lr.Globals.LrService.LocationResolver.Get.RefreshLocationResolver(storageId);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    // Official lr puts this function along with memory allocation for
    // lr IPC objects into a separate file, LocationResolverManagerFactory.
    private static SharedRef<ILocationResolverManager> GetLocationResolverManagerServiceObject(this LrClient lr)
    {
        using var manager = new SharedRef<ILocationResolverManager>();
        Result res = lr.Hos.Sm.GetService(ref manager.Ref, "lr");

        if (res.IsFailure())
        {
            throw new HorizonResultException(res, "Failed to get lr service object.");
        }

        return SharedRef<ILocationResolverManager>.CreateMove(ref manager.Ref);
    }
}