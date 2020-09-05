namespace LibHac.FsSrv.Sf
{
    public interface IWiper
    {
        public Result Startup(out long spaceToWipe);
        public Result Process(out long remainingSpaceToWipe);
    }
}
