global using GameCardHandle = System.UInt32;
using System;
using LibHac.Common.FixedArrays;

namespace LibHac.Fs;

public static class GameCard
{
    public static long GetGameCardSizeBytes(GameCardSizeInternal size) => size switch
    {
        GameCardSizeInternal.Size1Gb => 0x3B800000,
        GameCardSizeInternal.Size2Gb => 0x77000000,
        GameCardSizeInternal.Size4Gb => 0xEE000000,
        GameCardSizeInternal.Size8Gb => 0x1DC000000,
        GameCardSizeInternal.Size16Gb => 0x3B8000000,
        GameCardSizeInternal.Size32Gb => 0x770000000,
        _ => 0
    };

    public static long CardPageToOffset(int page)
    {
        return (long)page << 9;
    }
}

public enum GameCardSize
{
    // ReSharper disable InconsistentNaming
    Size1GB = 1,
    Size2GB = 2,
    Size4GB = 4,
    Size8GB = 8,
    Size16GB = 16,
    Size32GB = 32
    // ReSharper restore InconsistentNaming
}

public enum GameCardSizeInternal : byte
{
    Size1Gb = 0xFA,
    Size2Gb = 0xF8,
    Size4Gb = 0xF0,
    Size8Gb = 0xE0,
    Size16Gb = 0xE1,
    Size32Gb = 0xE2
}

public enum GameCardClockRate
{
    ClockRate25 = 25,
    ClockRate50 = 50
}

[Flags]
public enum GameCardAttribute : byte
{
    None = 0,
    AutoBootFlag = 1 << 0,
    HistoryEraseFlag = 1 << 1,
    RepairToolFlag = 1 << 2,
    DifferentRegionCupToTerraDeviceFlag = 1 << 3,
    DifferentRegionCupToGlobalDeviceFlag = 1 << 4,

    HasCa10CertificateFlag = 1 << 7
}

public enum GameCardCompatibilityType : byte
{
    Normal = 0,
    Terra = 1
}

public struct GameCardErrorInfo
{
    public ushort GameCardCrcErrorNum;
    public ushort Reserved2;
    public ushort AsicCrcErrorNum;
    public ushort Reserved6;
    public ushort RefreshNum;
    public ushort ReservedA;
    public ushort RetryLimitOutNum;
    public ushort TimeoutRetryNum;
}

public struct GameCardErrorReportInfo
{
    public ushort GameCardCrcErrorNum;
    public byte LastDeactivateReason;
    public byte Reserved3;
    public ushort AsicCrcErrorNum;
    public ushort Reserved6;
    public ushort RefreshNum;
    public ushort ReservedA;
    public ushort RetryLimitOutNum;
    public ushort TimeoutRetryNum;
    public ushort AsicReinitializeFailureDetail;
    public ushort InsertionCount;
    public ushort RemovalCount;
    public ushort AsicReinitializeNum;
    public uint AsicInitializeNum;
    public ushort AsicReinitializeFailureNum;
    public ushort AwakenFailureNum;
    public ushort Reserved20;
    public ushort RefreshSucceededCount;
    public uint LastReadErrorPageAddress;
    public uint LastReadErrorPageCount;
    public uint AwakenCount;
    public uint ReadCountFromInsert;
    public uint ReadCountFromAwaken;
    public uint LastDeactivateReasonResult;
    public Array4<byte> Reserved3C;
}

public struct GameCardUpdatePartitionInfo
{
    public uint CupVersion;
    public ulong CupId;
}