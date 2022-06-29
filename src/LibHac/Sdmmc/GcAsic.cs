using System;

namespace LibHac.Sdmmc;

public partial class SdmmcApi
{
    public const int GcAsicOperationSize = 0x40;

    public void PutGcAsicToSleep(Port port)
    {
        throw new NotImplementedException();
    }

    public Result AwakenGcAsic(Port port)
    {
        throw new NotImplementedException();
    }

    public Result WriteGcAsicOperation(Port port, ReadOnlySpan<byte> operationBuffer)
    {
        throw new NotImplementedException();
    }

    public Result FinishGcAsicOperation(Port port)
    {
        throw new NotImplementedException();
    }

    public Result AbortGcAsicOperation(Port port)
    {
        throw new NotImplementedException();
    }

    public Result SleepGcAsic(Port port)
    {
        throw new NotImplementedException();
    }

    public Result UpdateGcAsicKey(Port port)
    {
        throw new NotImplementedException();
    }

    public void SignalGcRemovedEvent(Port port)
    {
        throw new NotImplementedException();
    }

    public void ClearGcRemovedEvent(Port port)
    {
        throw new NotImplementedException();
    }
}