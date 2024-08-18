using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common.FixedArrays;

namespace LibHac.Gc.Impl;

public enum MakerCodeForCardId1 : byte
{
    MegaChips = 0xC2,
    Lapis = 0xAE
}

public enum MemoryCapacity : byte
{
    // ReSharper disable InconsistentNaming
    Capacity1GB = 0xFA,
    Capacity2GB = 0xF8,
    Capacity4GB = 0xF0,
    Capacity8GB = 0xE0,
    Capacity16GB = 0xE1,
    Capacity32GB = 0xE2
    // ReSharper restore InconsistentNaming
}

public enum MemoryType : byte
{
    T1RomFast = 0x01,
    T2RomFast = 0x02,
    T1NandFast = 0x09,
    T2NandFast = 0x0A,
    T1RomLate = 0x21,
    T2RomLate = 0x22,
    T1NandLate = 0x29,
    T2NandLate = 0x2A
}

public enum CardSecurityNumber : byte
{
    Number0 = 0,
    Number1 = 1,
    Number2 = 2,
    Number3 = 3,
    Number4 = 4
}

public enum CardType : byte
{
    Rom = 0,
    WritableDevT1 = 1,
    WritableProdT1 = 2,
    WritableDevT2 = 3,
    WritableProdT2 = 4
}

[StructLayout(LayoutKind.Sequential)]
public struct CardId1
{
    public MakerCodeForCardId1 MakerCode;
    public MemoryCapacity MemoryCapacity;
    public byte Reserved;
    public MemoryType MemoryType;
}

[StructLayout(LayoutKind.Sequential)]
public struct CardId2
{
    public CardSecurityNumber CardSecurityNumber;
    public CardType CardType;
    public Array2<byte> Reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct CardId3
{
    public Array4<byte> Reserved;
}

public enum MakerCodeForCardUid : byte
{
    Maker0 = 0,
    Maker1 = 1,
    Maker2 = 2,
}

public enum CardTypeForUid : byte
{
    WritableDev = 0xFE,
    WritableProd = 0xFF
}

[StructLayout(LayoutKind.Sequential)]
public struct CardUid
{
    public MakerCodeForCardUid MakerCode;
    public byte Version;
    public CardTypeForUid CardType;
    public Array9<byte> UniqueData;
    public uint Random;
    public byte PlatformFlag;
    public Array11<byte> Reserved;
    public CardId1 CardId1;
    public Array32<byte> Mac;
}

public enum DevCardRomSize : byte
{
    // ReSharper disable InconsistentNaming
    Size1GB = 3,
    Size2GB = 4,
    Size4GB = 5,
    Size8GB = 6,
    Size16GB = 7,
    Size32GB = 8
    // ReSharper restore InconsistentNaming
}

public enum DevCardNandSize : byte
{
    // ReSharper disable InconsistentNaming
    Size16GB = 7,
    Size32GB = 8,
    Size64GB = 9
    // ReSharper restore InconsistentNaming
}

[StructLayout(LayoutKind.Sequential)]
public struct DevCardParameter
{
    public CardId1 CardId1;
    public CardId2 CardId2;
    public CardId3 CardId3;
    public uint RomAreaStartAddr;
    public uint BackupAreaStartAddr;
    public Array3<byte> ReservedAreaStartAddr;
    public DevCardRomSize RomSize;
    public Array2<byte> WaitCycle1ForRead;
    public Array2<byte> WaitCycle2ForRead;
    public byte SpeedChangeEmulateWaitCycle1FrequencyForRead;
    public Array3<byte> SpeedChangeEmulateWaitCycle1ForRead;
    public byte SpeedChangeEmulateWaitCycle2FrequencyForRead;
    public Array3<byte> SpeedChangeEmulateWaitCycle2ForRead;
    public Array3<byte> FirstReadPageWaitCycleForRead;
    public Array2<byte> WaitCycle1ForWrite;
    public Array3<byte> WaitCycle2ForWrite;
    public byte SpeedChangeEmulateWaitCycle1FrequencyForWrite;
    public Array3<byte> SpeedChangeEmulateWaitCycle1ForWrite;
    public byte SpeedChangeEmulateWaitCycle2FrequencyForWrite;
    public Array3<byte> SpeedChangeEmulateWaitCycle2ForWrite;
    public Array2<byte> WaitCycle1ForSetAccessPattern;
    public Array3<byte> WaitCycle2ForSetAccessPattern;
    public Array3<byte> WaitCycleForRefresh;
    public Array3<byte> WaitCycleForSetKey;
    public Array3<byte> WaitCycleForIRdInit;
    public Array3<byte> WaitCycleForISetInit1;
    public Array3<byte> WaitCycleForISetGen;
    public Array3<byte> WaitCycleForISetInit2;
    public DevCardNandSize NandSize;
    public Array436<byte> Reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct CardInitialDataPayload
{
    public Array8<byte> PackageId;
    public Array8<byte> Reserved;
    public Array16<byte> AuthData;
    public Array16<byte> AuthMac;
    public Array12<byte> AuthNonce;
}

[StructLayout(LayoutKind.Sequential)]
public struct CardInitialData
{
    public CardInitialDataPayload Payload;
    public Array452<byte> Padding;
}

[StructLayout(LayoutKind.Sequential)]
public struct CardSpecificData
{
    public uint AsicSecurityMode;
    public uint AsicStatus;
    public CardId1 CardId1;
    public CardId2 CardId2;
    public Array64<byte> CardUid;
    public Array400<byte> Reserved;
    public Array32<byte> Mac;
}

public enum FwVersion : byte
{
    // ReSharper disable InconsistentNaming
    ForDev = 0,
    Since1_0_0 = 1,
    Since4_0_0 = 2,
    Since9_0_0 = 3,
    Since11_0_0 = 4,
    Since12_0_0 = 5
    // ReSharper restore InconsistentNaming
}

public enum KekIndex : byte
{
    Version0 = 0,
    ForDev = 1
}

[StructLayout(LayoutKind.Sequential)]
public struct CardHeaderEncryptedData
{
    public Array2<uint> FwVersion;
    public uint AccCtrl1;
    public uint Wait1TimeRead;
    public uint Wait2TimeRead;
    public uint Wait1TimeWrite;
    public uint Wait2TimeWrite;
    public uint FwMode;
    public uint CupVersion;
    public byte CompatibilityType;
    public byte Reserved25;
    public byte Reserved26;
    public byte Reserved27;
    public Array8<byte> UppHash;
    public ulong CupId;
    public Array56<byte> Reserved38;
}

public enum AccessControl1ClockRate
{
    ClockRate25MHz = 0xA10011,
    ClockRate50MHz = 0xA10010
}

public enum SelSec
{
    T1 = 1,
    T2 = 2
}

public enum BusPower
{
    // ReSharper disable InconsistentNaming
    Power_3_1V = 0,
    Power_1_8V = 1
    // ReSharper restore InconsistentNaming
}

[StructLayout(LayoutKind.Sequential)]
public struct CardHeader
{
    public static uint HeaderMagic => 0x44414548; // HEAD

    public uint Magic;
    public uint RomAreaStartPage;
    public uint BackupAreaStartPage;
    public byte KeyIndex;
    public byte RomSize;
    public byte Version;
    public byte Flags;
    public Array8<byte> PackageId;
    public uint ValidDataEndPage;
    public Array4<byte> Reserved11C;
    public Array16<byte> Iv;
    public long PartitionFsHeaderAddress;
    public long PartitionFsHeaderSize;
    public Array32<byte> PartitionFsHeaderHash;
    public Array32<byte> InitialDataHash;
    public uint SelSec;
    public uint SelT1Key;
    public uint SelKey;
    public uint LimAreaPage;
    public CardHeaderEncryptedData EncryptedData;

    public KekIndex KekIndex => (KekIndex)(KeyIndex & 0xF);
    public byte TitleKeyDecIndex => (byte)(KeyIndex >> 4);
}

[StructLayout(LayoutKind.Sequential)]
public struct CardHeaderWithSignature
{
    public Array256<byte> Signature;
    public CardHeader Data;
}

[StructLayout(LayoutKind.Sequential)]
public struct T1CardCertificate
{
    public static uint CertMagic => 0x54524543; // CERT

    public Array256<byte> Signature;
    public uint Magic;
    public uint Version;
    public byte KekIndex;
    public Array7<byte> Flags;
    public Array16<byte> T1CardDeviceId;
    public Array16<byte> Iv;
    public Array16<byte> HwKey;
    public Array192<byte> Reserved;
    public Array512<byte> Padding;
}

[StructLayout(LayoutKind.Sequential)]
public struct Ca10Certificate
{
    public Array256<byte> Signature;
    public Array48<byte> Unk100;
    public Array256<byte> Modulus;
    public Array464<byte> Unk230;
}

[StructLayout(LayoutKind.Sequential)]
public struct CardInitReceiveData
{
    public uint CupVersion;
    public CardId1 CardId1;
}

[StructLayout(LayoutKind.Sequential)]
public struct CardSecurityInformation
{
    public uint MemoryInterfaceMode;
    public uint AsicStatus;
    public CardId1 Id1;
    public CardId2 Id2;
    public CardUid Uid;
    public Array400<byte> Reserved;
    public Array32<byte> Mac;
    public Array1024<byte> CardCertificate;
    public CardInitialData InitialData;

    [UnscopedRef]
    public ref T1CardCertificate T1Certificate =>
        ref Unsafe.As<byte, T1CardCertificate>(ref MemoryMarshal.GetReference(CardCertificate[..]));
}

public enum AsicFirmwareType : byte
{
    Read = 0,
    Write = 1
}

public enum AsicState : byte
{
    Initial = 0,
    Secure = 1
}

public enum GameCardMode : byte
{
    Initial = 0,
    Normal = 1,
    Secure = 2,
    Debug = 3
}

[StructLayout(LayoutKind.Sequential)]
public struct TotalAsicInfo
{
    public uint InitializeCount;
    public uint AwakenCount;
    public ushort AwakenFailureCount;
    public Array2<byte> Reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct CardAccessInternalInfo
{
    public ushort RetryLimitOutNum;
    public ushort AsicReinitializeCount;
    public ushort AsicReinitializeFailureNum;
    public ushort RefreshSuccessCount;
    public uint LastReadErrorPageAddress;
    public uint LastReadErrorPageCount;
    public uint ReadCountFromInsert;
    public uint ReadCountFromAwaken;
    public Result LastAsicReinitializeFailureResult;
}