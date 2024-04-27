using System;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.Lr;

public interface IAddOnContentLocationResolver : IDisposable
{
    Result ResolveAddOnContentPath(out Path outPath, DataId id);
    Result RegisterAddOnContentStorage(DataId id, Ncm.ApplicationId applicationId, StorageId storageId);
    Result UnregisterAllAddOnContentPath();
    Result RefreshApplicationAddOnContent(InArray<Ncm.ApplicationId> ids);
    Result UnregisterApplicationAddOnContent(Ncm.ApplicationId id);
    Result GetRegisteredAddOnContentPaths(out Path outPath, out Path outPatchPath, DataId id);
    Result RegisterAddOnContentPath(DataId id, ApplicationId applicationId, in Path path);
    Result RegisterAddOnContentPaths(DataId id, ApplicationId applicationId, in Path path, in Path patchPath);
}