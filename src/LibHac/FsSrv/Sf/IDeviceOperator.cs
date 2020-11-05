using System;

namespace LibHac.FsSrv.Sf
{
    public interface IDeviceOperator : IDisposable
    {
        Result IsSdCardInserted(out bool isInserted);
        Result IsGameCardInserted(out bool isInserted);
        Result GetGameCardHandle(out GameCardHandle handle);
    }
}