using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Ncm;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    public struct SaveDataAttribute : IEquatable<SaveDataAttribute>, IComparable<SaveDataAttribute>
    {
        [FieldOffset(0x00)] public TitleId TitleId;
        [FieldOffset(0x08)] public UserId UserId;
        [FieldOffset(0x18)] public ulong SaveDataId;
        [FieldOffset(0x20)] public SaveDataType Type;
        [FieldOffset(0x21)] public SaveDataRank Rank;
        [FieldOffset(0x22)] public short Index;

        public override bool Equals(object obj)
        {
            return obj is SaveDataAttribute attribute && Equals(attribute);
        }

        public bool Equals(SaveDataAttribute other)
        {
            return TitleId == other.TitleId &&
                   Type == other.Type &&
                   UserId.Equals(other.UserId) &&
                   SaveDataId == other.SaveDataId &&
                   Rank == other.Rank &&
                   Index == other.Index;
        }

        public override int GetHashCode()
        {
            // ReSharper disable NonReadonlyMemberInGetHashCode
            return HashCode.Combine(TitleId, Type, UserId, SaveDataId, Rank, Index);
            // ReSharper restore NonReadonlyMemberInGetHashCode
        }

        public int CompareTo(SaveDataAttribute other)
        {
            int titleIdComparison = TitleId.CompareTo(other.TitleId);
            if (titleIdComparison != 0) return titleIdComparison;
            int typeComparison = Type.CompareTo(other.Type);
            if (typeComparison != 0) return typeComparison;
            int userIdComparison = UserId.CompareTo(other.UserId);
            if (userIdComparison != 0) return userIdComparison;
            int saveDataIdComparison = SaveDataId.CompareTo(other.SaveDataId);
            if (saveDataIdComparison != 0) return saveDataIdComparison;
            int rankComparison = Rank.CompareTo(other.Rank);
            if (rankComparison != 0) return rankComparison;
            return Index.CompareTo(other.Index);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x48)]
    public struct SaveDataFilter
    {
        [FieldOffset(0x00)] public bool FilterByProgramId;
        [FieldOffset(0x01)] public bool FilterBySaveDataType;
        [FieldOffset(0x02)] public bool FilterByUserId;
        [FieldOffset(0x03)] public bool FilterBySaveDataId;
        [FieldOffset(0x04)] public bool FilterByIndex;
        [FieldOffset(0x05)] public SaveDataRank Rank;

        [FieldOffset(0x08)] public TitleId ProgramId;
        [FieldOffset(0x10)] public UserId UserId;
        [FieldOffset(0x20)] public ulong SaveDataId;
        [FieldOffset(0x28)] public SaveDataType SaveDataType;
        [FieldOffset(0x2A)] public short Index;

        public void SetProgramId(TitleId value)
        {
            FilterByProgramId = true;
            ProgramId = value;
        }

        public void SetSaveDataType(SaveDataType value)
        {
            FilterBySaveDataType = true;
            SaveDataType = value;
        }

        public void SetUserId(UserId value)
        {
            FilterByUserId = true;
            UserId = value;
        }

        public void SetSaveDataId(ulong value)
        {
            FilterBySaveDataId = true;
            SaveDataId = value;
        }

        public void SetIndex(short value)
        {
            FilterByIndex = true;
            Index = value;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = HashLength)]
    public struct HashSalt
    {
        private const int HashLength = 0x20;

        [FieldOffset(0x00)] private byte _hashStart;

        public Span<byte> Hash => SpanHelpers.CreateSpan(ref _hashStart, HashLength);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct OptionalHashSalt
    {
        public bool IsSet;
        public HashSalt HashSalt;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public struct SaveMetaCreateInfo
    {
        [FieldOffset(0)] public int Size;
        [FieldOffset(4)] public SaveDataMetaType Type;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    public struct SaveDataCreationInfo
    {
        [FieldOffset(0x00)] public long Size;
        [FieldOffset(0x08)] public long JournalSize;
        [FieldOffset(0x10)] public ulong BlockSize;
        [FieldOffset(0x18)] public TitleId OwnerId;
        [FieldOffset(0x20)] public SaveDataFlags Flags;
        [FieldOffset(0x24)] public SaveDataSpaceId SpaceId;
        [FieldOffset(0x25)] public bool Field25;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x60)]
    public struct SaveDataInfo
    {
        [FieldOffset(0x00)] public ulong SaveDataId;
        [FieldOffset(0x08)] public SaveDataSpaceId SpaceId;
        [FieldOffset(0x09)] public SaveDataType Type;
        [FieldOffset(0x10)] public UserId UserId;
        [FieldOffset(0x20)] public ulong SaveDataIdFromKey;
        [FieldOffset(0x28)] public TitleId TitleId;
        [FieldOffset(0x30)] public long Size;
        [FieldOffset(0x38)] public short Index;
        [FieldOffset(0x3A)] public SaveDataRank Rank;
        [FieldOffset(0x3B)] public SaveDataState State;
    }
}
