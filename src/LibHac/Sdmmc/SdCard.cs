using System;

namespace LibHac.Sdmmc;

public enum SdCardSwitchFunction
{
    CheckSupportedFunction = 0,
    CheckDefault = 1,
    CheckHighSpeed = 2,
    CheckSdr50 = 3,
    CheckSdr104 = 4,
    CheckDdr50 = 5
};

public partial class SdmmcApi
{
    public const int SdCardScrSize = 8;
    public const int SdCardSwitchFunctionStatusSize = 0x40;
    public const int SdCardSdStatusSize = 0x40;

    public const int SdCardWorkBufferSize = SdCardSdStatusSize;

    public void SetSdCardWorkBuffer(Port port, Memory<byte> workBuffer)
    {
        throw new NotImplementedException();
    }

    public void PutSdCardToSleep(Port port)
    {
        throw new NotImplementedException();
    }

    public void AwakenSdCard(Port port)
    {
        throw new NotImplementedException();
    }

    public Result GetSdCardProtectedAreaCapacity(out uint outNumSectors, Port port)
    {
        throw new NotImplementedException();
    }

    public Result GetSdCardScr(Span<byte> outBuffer, Port port)
    {
        throw new NotImplementedException();
    }

    public Result GetSdCardSwitchFunctionStatus(Span<byte> outBuffer, Port port, SdCardSwitchFunction switchFunction)
    {
        throw new NotImplementedException();
    }

    public Result GetSdCardCurrentConsumption(out ushort outCurrentConsumption, Port port, SpeedMode speedMode)
    {
        throw new NotImplementedException();
    }

    public Result GetSdCardSdStatus(Span<byte> outBuffer, Port port)
    {
        throw new NotImplementedException();
    }

    public Result CheckSdCardConnection(out SpeedMode outSpeedMode, out BusWidth outBusWidth, Port port)
    {
        throw new NotImplementedException();
    }

    public void RegisterSdCardDetectionEventCallback(Port port, DeviceDetectionEventCallback callback, object args)
    {
        throw new NotImplementedException();
    }

    public void UnregisterSdCardDetectionEventCallback(Port port)
    {
        throw new NotImplementedException();
    }

    public bool IsSdCardInserted(Port port)
    {
        throw new NotImplementedException();
    }

    public bool IsSdCardRemoved(Port port)
    {
        throw new NotImplementedException();
    }
}