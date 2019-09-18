using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs.Save;
using LibHac.Ncm;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    public struct SaveDataAttribute2
    {
        [FieldOffset(0x00)] public ulong TitleId;
        [FieldOffset(0x08)] public UserId UserId;
        [FieldOffset(0x18)] public ulong SaveDataId;
        [FieldOffset(0x20)] public SaveDataType Type;
        [FieldOffset(0x21)] public byte Rank;
        [FieldOffset(0x22)] public short Index;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x48)]
    public struct SaveDataFilter
    {
        [FieldOffset(0x00)] public bool FilterByTitleId;
        [FieldOffset(0x01)] public bool FilterBySaveDataType;
        [FieldOffset(0x02)] public bool FilterByUserId;
        [FieldOffset(0x03)] public bool FilterBySaveDataId;
        [FieldOffset(0x04)] public bool FilterByIndex;
        [FieldOffset(0x05)] public byte Rank;

        [FieldOffset(0x08)] public TitleId TitleID;
        [FieldOffset(0x10)] public UserId UserId;
        [FieldOffset(0x20)] public ulong SaveDataId;
        [FieldOffset(0x28)] public SaveDataType SaveDataType;
        [FieldOffset(0x2A)] public short Index;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x50)]
    public struct SaveDataFilterInternal
    {
        [FieldOffset(0x00)] public bool FilterBySaveDataSpaceId;
        [FieldOffset(0x01)] public SaveDataSpaceId SpaceId;

        [FieldOffset(0x08)] public bool FilterByTitleId;
        [FieldOffset(0x10)] public TitleId TitleID;

        [FieldOffset(0x18)] public bool FilterBySaveDataType;
        [FieldOffset(0x19)] public SaveDataType SaveDataType;

        [FieldOffset(0x20)] public bool FilterByUserId;
        [FieldOffset(0x28)] public UserId UserId;

        [FieldOffset(0x38)] public bool FilterBySaveDataId;
        [FieldOffset(0x40)] public ulong SaveDataId;

        [FieldOffset(0x48)] public bool FilterByIndex;
        [FieldOffset(0x4A)] public short Index;

        [FieldOffset(0x4C)] public int Rank;
    }

    [StructLayout(LayoutKind.Explicit, Size = HashLength)]
    public struct HashSalt
    {
        private const int HashLength = 0x20;

        [FieldOffset(0x00)] private byte _hashStart;

        public Span<byte> Hash => SpanHelpers.CreateSpan(ref _hashStart, HashLength);
    }
}
