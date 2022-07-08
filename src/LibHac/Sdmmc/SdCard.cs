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

    private bool _isSdCardInserted;
    private bool _isSdCardRemoved;

    public void SetSdCardInserted(bool isInserted)
    {
        if (_isSdCardInserted && isInserted == false)
        {
            _isSdCardRemoved = true;
        }

        _isSdCardInserted = isInserted;
    }

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

    }

    public void UnregisterSdCardDetectionEventCallback(Port port)
    {

    }

    public bool IsSdCardInserted(Port port)
    {
        return _isSdCardInserted;
    }

    public bool IsSdCardRemoved(Port port)
    {
        return _isSdCardRemoved;
    }
}