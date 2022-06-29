using System;

namespace LibHac.Sdmmc;

public enum MmcPartition
{
    UserData = 0,
    BootPartition1 = 1,
    BootPartition2 = 2,
    Unknown = 3
}

public partial class SdmmcApi
{
    public const int MmcExtendedCsdSize = 0x200;
    public const int MmcWorkBufferSize = MmcExtendedCsdSize;

    public void SetMmcWorkBuffer(Port port, Memory<byte> workBuffer)
    {
        throw new NotImplementedException();
    }

    public void PutMmcToSleep(Port port)
    {
        throw new NotImplementedException();
    }

    public void AwakenMmc(Port port)
    {
        throw new NotImplementedException();
    }

    public Result SelectMmcPartition(Port port, MmcPartition mmcPartition)
    {
        throw new NotImplementedException();
    }

    public Result EraseMmc(Port port)
    {
        throw new NotImplementedException();
    }

    public Result GetMmcBootPartitionCapacity(out uint outNumSectors, Port port)
    {
        throw new NotImplementedException();
    }

    public Result GetMmcExtendedCsd(Span<byte> outBuffer, Port port)
    {
        throw new NotImplementedException();
    }

    public Result CheckMmcConnection(out SpeedMode outSpeedMode, out BusWidth outBusWidth, Port port)
    {
        throw new NotImplementedException();
    }
}