using System;
using LibHac.Ncm;
using LibHac.Sf;

namespace LibHac.Lr
{
    public class AddOnContentLocationResolver : IDisposable
    {
        private ReferenceCountedDisposable<IAddOnContentLocationResolver> _interface;

        public AddOnContentLocationResolver(ReferenceCountedDisposable<IAddOnContentLocationResolver> baseInterface)
        {
            _interface = baseInterface.AddReference();
        }

        public Result ResolveAddOnContentPath(out Path path, DataId id) =>
            _interface.Target.ResolveAddOnContentPath(out path, id);

        public Result RegisterAddOnContentStorage(DataId id, Ncm.ApplicationId applicationId, StorageId storageId) =>
            _interface.Target.RegisterAddOnContentStorage(id, applicationId, storageId);

        public Result UnregisterAllAddOnContentPath() =>
            _interface.Target.UnregisterAllAddOnContentPath();

        public Result RefreshApplicationAddOnContent(InArray<Ncm.ApplicationId> ids) =>
            _interface.Target.RefreshApplicationAddOnContent(ids);

        public Result UnregisterApplicationAddOnContent(Ncm.ApplicationId id) =>
            _interface.Target.UnregisterApplicationAddOnContent(id);

        public void Dispose()
        {
            _interface?.Dispose();
        }
    }
}
