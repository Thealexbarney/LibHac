using System;
using LibHac.FsSrv.Sf;
using LibHac.Sf;

namespace LibHac.FsSrv.Impl
{
    internal class BisWiper : IWiper
    {
        // ReSharper disable UnusedParameter.Local
        public BisWiper(NativeHandle memoryHandle, ulong memorySize) { }
        // ReSharper restore UnusedParameter.Local

        public Result Startup(out long spaceToWipe)
        {
            throw new NotImplementedException();
        }

        public Result Process(out long remainingSpaceToWipe)
        {
            throw new NotImplementedException();
        }

        public static Result CreateWiper(out IWiper wiper, NativeHandle memoryHandle, ulong memorySize)
        {
            wiper = new BisWiper(memoryHandle, memorySize);
            return Result.Success;
        }

        public void Dispose()
        {
        }
    }
}
