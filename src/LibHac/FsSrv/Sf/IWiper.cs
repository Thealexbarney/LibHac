using System;

namespace LibHac.FsSrv.Sf
{
    public interface IWiper : IDisposable
    {
        public Result Startup(out long spaceToWipe);
        public Result Process(out long remainingSpaceToWipe);
    }
}
