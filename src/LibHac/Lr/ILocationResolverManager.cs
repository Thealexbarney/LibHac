using System;
using LibHac.Ncm;

namespace LibHac.Lr
{
    public interface ILocationResolverManager : IDisposable
    {
        Result OpenLocationResolver(out ReferenceCountedDisposable<ILocationResolver> resolver, StorageId storageId);
        Result OpenRegisteredLocationResolver(out ReferenceCountedDisposable<IRegisteredLocationResolver> resolver);
        Result RefreshLocationResolver(StorageId storageId);
        Result OpenAddOnContentLocationResolver(out ReferenceCountedDisposable<IAddOnContentLocationResolver> resolver);
    }
}
