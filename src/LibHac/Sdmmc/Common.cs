using System;
using LibHac.FsSrv;

namespace LibHac.Sdmmc;

public enum BusPower
{
    // ReSharper disable InconsistentNaming
    PowerOff = 0,
    Power1_8V = 1,
    Power3_3V = 2,
    // ReSharper restore InconsistentNaming
}
public enum BusWidth
{
    Width1Bit = 0,
    Width4Bit = 1,
    Width8Bit = 2,
}

public enum SpeedMode
{
    MmcIdentification = 0,
    MmcLegacySpeed = 1,
    MmcHighSpeed = 2,
    MmcHs200 = 3,
    MmcHs400 = 4,
    SdCardIdentification = 5,
    SdCardDefaultSpeed = 6,
    SdCardHighSpeed = 7,
    SdCardSdr12 = 8,
    SdCardSdr25 = 9,
    SdCardSdr50 = 10,
    SdCardSdr104 = 11,
    SdCardDdr50 = 12,
    GcAsicFpgaSpeed = 13,
    GcAsicSpeed = 14
}

public enum Port
{
    Mmc0 = 0,
    SdCard0 = 1,
    GcAsic0 = 2
}

public struct ErrorInfo
{
    public uint NumActivationFailures;
    public uint NumActivationErrorCorrections;
    public uint NumReadWriteFailures;
    public uint NumReadWriteErrorCorrections;
}

public delegate void DeviceDetectionEventCallback(object args);

public partial class SdmmcApi
{
    public const int SectorSize = 0x200;

    public const int DeviceCidSize = 0x10;
    public const int DeviceCsdSize = 0x10;

    private FileSystemServer _fsServer;
    internal HorizonClient Hos => _fsServer.Hos;

    public SdmmcApi(FileSystemServer fsServer)
    {
        _fsServer = fsServer;
    }

    public void SwitchToPcvClockResetControl()
    {
        throw new NotImplementedException();
    }

    public void Initialize(Port port)
    {
        throw new NotImplementedException();
    }

    public void Finalize(Port port)
    {
        throw new NotImplementedException();
    }

    public void ChangeCheckTransferInterval(Port port, uint ms)
    {
        throw new NotImplementedException();
    }

    public void SetDefaultCheckTransferInterval(Port port)
    {
        throw new NotImplementedException();
    }

    public Result Activate(Port port)
    {
        throw new NotImplementedException();
    }

    public void Deactivate(Port port)
    {
        throw new NotImplementedException();
    }

    public Result Read(Span<byte> destination, Port port, uint sectorIndex, uint sectorCount)
    {
        throw new NotImplementedException();
    }

    public Result Write(Port port, uint sectorIndex, uint sectorCount, ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }

    public Result CheckConnection(out SpeedMode outSpeedMode, out BusWidth outBusWidth, Port port)
    {
        throw new NotImplementedException();
    }

    public Result GetDeviceSpeedMode(out SpeedMode outSpeedMode, Port port)
    {
        throw new NotImplementedException();
    }

    public Result GetDeviceMemoryCapacity(out uint outNumSectors, Port port)
    {
        throw new NotImplementedException();
    }

    public Result GetDeviceStatus(out uint outDeviceStatus, Port port)
    {
        throw new NotImplementedException();
    }

    public Result GetDeviceCid(Span<byte> outBuffer, Port port)
    {
        throw new NotImplementedException();
    }

    public Result GetDeviceCsd(Span<byte> outBuffer, Port port)
    {
        throw new NotImplementedException();
    }

    public void GetAndClearErrorInfo(out ErrorInfo outErrorInfo, out int outLogSize, Span<byte> outLogBuffer, Port port)
    {
        throw new NotImplementedException();
    }

    public void RegisterDeviceVirtualAddress(Port port, Memory<byte> buffer, ulong bufferDeviceVirtualAddress)
    {
        throw new NotImplementedException();
    }

    public void UnregisterDeviceVirtualAddress(Port port, Memory<byte> buffer, ulong bufferDeviceVirtualAddress)
    {
        throw new NotImplementedException();
    }
}