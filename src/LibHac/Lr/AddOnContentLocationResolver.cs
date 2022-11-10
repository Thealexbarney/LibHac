using System;
using LibHac.Common;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.Lr;

public class AddOnContentLocationResolver : IDisposable
{
    private SharedRef<IAddOnContentLocationResolver> _interface;

    public AddOnContentLocationResolver(ref SharedRef<IAddOnContentLocationResolver> baseInterface)
    {
        _interface = SharedRef<IAddOnContentLocationResolver>.CreateMove(ref baseInterface);
    }

    public void Dispose()
    {
        _interface.Destroy();
    }

    public Result ResolveAddOnContentPath(out Path path, DataId id) =>
        _interface.Get.ResolveAddOnContentPath(out path, id);

    public Result RegisterAddOnContentStorage(DataId id, Ncm.ApplicationId applicationId, StorageId storageId) =>
        _interface.Get.RegisterAddOnContentStorage(id, applicationId, storageId);

    public Result UnregisterAllAddOnContentPath() =>
        _interface.Get.UnregisterAllAddOnContentPath();

    public Result RefreshApplicationAddOnContent(InArray<Ncm.ApplicationId> ids) =>
        _interface.Get.RefreshApplicationAddOnContent(ids);

    public Result UnregisterApplicationAddOnContent(Ncm.ApplicationId id) =>
        _interface.Get.UnregisterApplicationAddOnContent(id);
}