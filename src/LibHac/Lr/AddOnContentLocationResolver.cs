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

    public Result GetRegisteredAddOnContentPaths(out Path outPath, out Path outPatchPath, DataId id) =>
        _interface.Get.GetRegisteredAddOnContentPaths(out outPath, out outPatchPath, id);

    public Result RegisterAddOnContentPath(DataId id, ApplicationId applicationId, in Path path) =>
        _interface.Get.RegisterAddOnContentPath(id, applicationId, in path);

    public Result RegisterAddOnContentPaths(DataId id, ApplicationId applicationId, in Path path, in Path patchPath) =>
        _interface.Get.RegisterAddOnContentPaths(id, applicationId, in path, in patchPath);
}