namespace LibHac.FsService
{
    public static class ResultSdmmc
    {
        public const int ModuleSdmmc = 24;

        public static Result.Base DeviceNotFound => new Result.Base(ModuleSdmmc, 1);
        public static Result.Base DeviceAsleep => new Result.Base(ModuleSdmmc, 4);
    }
}
