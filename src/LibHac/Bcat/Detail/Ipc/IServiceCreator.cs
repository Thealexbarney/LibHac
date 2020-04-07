using LibHac.Ncm;

namespace LibHac.Bcat.Detail.Ipc
{
    public interface IServiceCreator
    {
        Result CreateDeliveryCacheStorageServiceWithApplicationId(out IDeliveryCacheStorageService service,
            TitleId applicationId);
    }
}
