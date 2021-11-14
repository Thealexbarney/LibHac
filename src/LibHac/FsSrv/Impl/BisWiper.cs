using System;
using LibHac.Common;
using LibHac.FsSrv.Sf;
using LibHac.Sf;

namespace LibHac.FsSrv.Impl;

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

    public static Result CreateWiper(ref UniqueRef<IWiper> outWiper, NativeHandle memoryHandle, ulong memorySize)
    {
        outWiper.Reset(new BisWiper(memoryHandle, memorySize));
        return Result.Success;
    }

    public void Dispose()
    {
    }
}
