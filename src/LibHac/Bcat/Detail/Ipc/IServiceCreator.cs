using LibHac.Ncm;

namespace LibHac.Bcat.Detail.Ipc
{
    public interface IServiceCreator
    {
        Result CreateDeliveryCacheStorageService(out IDeliveryCacheStorageService service,
            ulong processId);

        Result CreateDeliveryCacheStorageServiceWithApplicationId(out IDeliveryCacheStorageService service,
            ApplicationId applicationId);
    }
}
