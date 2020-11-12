using System;
using LibHac.Sf;

namespace LibHac.FsSrv.Sf
{
    public interface IEventNotifier : IDisposable
    {
        Result GetEventHandle(out NativeHandle handle);
    }
}
