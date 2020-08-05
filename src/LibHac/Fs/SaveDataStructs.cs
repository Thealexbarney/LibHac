using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Ncm;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    public struct SaveDataAttribute : IEquatable<SaveDataAttribute>, IComparable<SaveDataAttribute>
    {
        [FieldOffset(0x00)] public ProgramId ProgramId;
        [FieldOffset(0x08)] public UserId UserId;
        [FieldOffset(0x18)] public ulong StaticSaveDataId;
        [FieldOffset(0x20)] public SaveDataType Type;
        [FieldOffset(0x21)] public SaveDataRank Rank;
        [FieldOffset(0x22)] public short Index;

        public SaveDataAttribute(ProgramId programId, SaveDataType type, UserId userId, ulong saveDataId) : this(
            programId, type, userId, saveDataId, 0, SaveDataRank.Primary)
        { }

        public SaveDataAttribute(ProgramId programId, SaveDataType type, UserId userId, ulong saveDataId,
            short index) : this(programId, type, userId, saveDataId, index, SaveDataRank.Primary)
        { }

        public SaveDataAttribute(ProgramId programId, SaveDataType type, UserId userId, ulong saveDataId, short index,
            SaveDataRank rank)
        {
            ProgramId = programId;
            Type = type;
            UserId = userId;
            StaticSaveDataId = saveDataId;
            Index = index;
            Rank = rank;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is SaveDataAttribute attribute && Equals(attribute);
        }

        public readonly bool Equals(SaveDataAttribute other)
        {
            return ProgramId == other.ProgramId &&
                   Type == other.Type &&
                   UserId.Equals(other.UserId) &&
                   StaticSaveDataId == other.StaticSaveDataId &&
                   Rank == other.Rank &&
                   Index == other.Index;
        }

        public static bool operator ==(SaveDataAttribute left, SaveDataAttribute right) => left.Equals(right);
        public static bool operator !=(SaveDataAttribute left, SaveDataAttribute right) => !(left == right);

        public override readonly int GetHashCode()
        {
            // ReSharper disable NonReadonlyMemberInGetHashCode
            return HashCode.Combine(ProgramId, Type, UserId, StaticSaveDataId, Rank, Index);
            // ReSharper restore NonReadonlyMemberInGetHashCode
        }

        public readonly int CompareTo(SaveDataAttribute other)
        {
            int titleIdComparison = ProgramId.CompareTo(other.ProgramId);
            if (titleIdComparison != 0) return titleIdComparison;
            int typeComparison = ((int)Type).CompareTo((int)other.Type);
            if (typeComparison != 0) return typeComparison;
            int userIdComparison = UserId.CompareTo(other.UserId);
            if (userIdComparison != 0) return userIdComparison;
            int saveDataIdComparison = StaticSaveDataId.CompareTo(other.StaticSaveDataId);
            if (saveDataIdComparison != 0) return saveDataIdComparison;
            int rankComparison = ((int)Rank).CompareTo((int)other.Rank);
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

        [FieldOffset(0x08)] public ProgramId ProgramId;
        [FieldOffset(0x10)] public UserId UserId;
        [FieldOffset(0x20)] public ulong SaveDataId;
        [FieldOffset(0x28)] public SaveDataType SaveDataType;
        [FieldOffset(0x2A)] public short Index;

        public void SetProgramId(ProgramId value)
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
        [FieldOffset(0x18)] public ulong OwnerId;
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
        [FieldOffset(0x20)] public ulong StaticSaveDataId;
        [FieldOffset(0x28)] public ProgramId ProgramId;
        [FieldOffset(0x30)] public long Size;
        [FieldOffset(0x38)] public short Index;
        [FieldOffset(0x3A)] public SaveDataRank Rank;
        [FieldOffset(0x3B)] public SaveDataState State;
    }
}
