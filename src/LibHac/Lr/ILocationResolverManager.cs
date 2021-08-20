using System;
using LibHac.Common;
using LibHac.Ncm;

namespace LibHac.Lr
{
    public interface ILocationResolverManager : IDisposable
    {
        Result OpenLocationResolver(ref SharedRef<ILocationResolver> outResolver, StorageId storageId);
        Result OpenRegisteredLocationResolver(ref SharedRef<IRegisteredLocationResolver> outResolver);
        Result RefreshLocationResolver(StorageId storageId);
        Result OpenAddOnContentLocationResolver(ref SharedRef<IAddOnContentLocationResolver> outResolver);
    }
}
