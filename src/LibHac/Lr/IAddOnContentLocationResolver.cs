using System;
using LibHac.Ncm;

namespace LibHac.Lr
{
    public interface IAddOnContentLocationResolver : IDisposable
    {
        Result ResolveAddOnContentPath(out Path path, DataId id);
        Result RegisterAddOnContentStorage(DataId id, Ncm.ApplicationId applicationId, StorageId storageId);
        Result UnregisterAllAddOnContentPath();
        Result RefreshApplicationAddOnContent(ReadOnlySpan<Ncm.ApplicationId> ids);
        Result UnregisterApplicationAddOnContent(Ncm.ApplicationId id);
    }
}
