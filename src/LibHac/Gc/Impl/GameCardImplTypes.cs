using LibHac.Common.FixedArrays;

namespace LibHac.Gc.Impl;

public struct CardId1
{
    public byte MakerCode;
    public MemoryCapacity MemoryCapacity;
    public byte Reserved;
    public byte MemoryType;
}

public struct CardId2
{
    public byte CardSecurityNumber;
    public byte CardType;
    public Array2<byte> Reserved;
}

public struct CardId3
{
    public Array4<byte> Reserved;
}

public struct CardInitialDataPayload
{
    public Array8<byte> PackageId;
    public Array8<byte> Reserved;
    public Array16<byte> AuthData;
    public Array16<byte> AuthMac;
    public Array12<byte> AuthNonce;
}

public struct CardInitialData
{
    public CardInitialDataPayload Payload;
    public Array452<byte> Padding;
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
    public ulong PartitionFsHeaderAddress;
    public ulong PartitionFsHeaderSize;
    public Array32<byte> PartitionFsHeaderHash;
    public Array32<byte> InitialDataHash;
    public uint SelSec;
    public uint SelT1Key;
    public uint SelKey;
    public uint LimAreaPage;
    public CardHeaderEncryptedData EncryptedData;
}

public struct CardHeaderWithSignature
{
    public Array256<byte> Signature;
    public CardHeader Data;
}

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

public struct Ca10Certificate
{
    public Array256<byte> Signature;
    public Array48<byte> Unk100;
    public Array256<byte> Modulus;
    public Array464<byte> Unk230;
}