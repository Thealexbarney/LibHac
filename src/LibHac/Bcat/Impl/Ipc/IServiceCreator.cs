namespace LibHac.Bcat.Impl.Ipc
{
    public interface IServiceCreator
    {
        Result CreateDeliveryCacheStorageService(out IDeliveryCacheStorageService service,
            ulong processId);

        Result CreateDeliveryCacheStorageServiceWithApplicationId(out IDeliveryCacheStorageService service,
            ApplicationId applicationId);
    }
}
