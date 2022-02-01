using LibHac.Common.FixedArrays;
using LibHac.Gc.Impl;

namespace LibHac.Gc;

public struct GameCardStatus
{
    public Array32<byte> PartitionFsHeaderHash;
    public ulong PackageId;
    public long Size;
    public long PartitionFsHeaderOffset;
    public long PartitionFsHeaderSize;
    public long SecureAreaOffset;
    public long SecureAreaSize;
    public uint UpdatePartitionVersion;
    public ulong UpdatePartitionId;
    public byte CompatibilityType;
    public Array3<byte> Reserved61;
    public byte GameCardAttribute;
    public Array11<byte> Reserved65;
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