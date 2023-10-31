using LibHac.Common.FixedArrays;
using LibHac.Gc.Impl;

namespace LibHac.Gc;

public struct GameCardStatus
{
    public Array32<byte> PartitionFsHeaderHash;
    public Array8<byte>  PackageId;
    public long CardSize;
    public long PartitionFsHeaderAddress;
    public long PartitionFsHeaderSize;
    public long NormalAreaSize;
    public long SecureAreaSize;
    public uint CupVersion;
    public ulong CupId;
    public byte CompatibilityType;
    public Array3<byte> Reserved1;
    public byte Flags;
    public Array11<byte> Reserved2;
}

public struct RmaInformation
{
    public Array512<byte> Data;
}

public struct GameCardIdSet
{
    public CardId1 Id1;
    public CardId2 Id2;
    public CardId3 Id3;
}

public struct GameCardAsicCertificateSet
{
    public Array1024<byte> Certificate;
    public Array16<byte> SerialNumber;
    public Array256<byte> PublicKeyModulus;
    public Array3<byte> PublicKeyExponent;
}