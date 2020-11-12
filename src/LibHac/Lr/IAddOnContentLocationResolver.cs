using System;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.Lr
{
    public interface IAddOnContentLocationResolver : IDisposable
    {
        Result ResolveAddOnContentPath(out Path path, DataId id);
        Result RegisterAddOnContentStorage(DataId id, Ncm.ApplicationId applicationId, StorageId storageId);
        Result UnregisterAllAddOnContentPath();
        Result RefreshApplicationAddOnContent(InArray<Ncm.ApplicationId> ids);
        Result UnregisterApplicationAddOnContent(Ncm.ApplicationId id);
    }
}
