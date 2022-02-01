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