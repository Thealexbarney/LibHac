namespace LibHac.FsSrv
{
    public interface IDeviceOperator
    {
        Result IsSdCardInserted(out bool isInserted);
        Result IsGameCardInserted(out bool isInserted);
        Result GetGameCardHandle(out GameCardHandle handle);
    }
}