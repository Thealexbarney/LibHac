using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.FsSrv.Impl;
using LibHac.Ncm;
using LibHac.Util;

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
        [FieldOffset(0x22)] public ushort Index;

        public SaveDataAttribute(ProgramId programId, SaveDataType type, UserId userId, ulong saveDataId) : this(
            programId, type, userId, saveDataId, 0, SaveDataRank.Primary)
        { }

        public SaveDataAttribute(ProgramId programId, SaveDataType type, UserId userId, ulong saveDataId,
            ushort index) : this(programId, type, userId, saveDataId, index, SaveDataRank.Primary)
        { }

        public SaveDataAttribute(ProgramId programId, SaveDataType type, UserId userId, ulong saveDataId, ushort index,
            SaveDataRank rank)
        {
            ProgramId = programId;
            Type = type;
            UserId = userId;
            StaticSaveDataId = saveDataId;
            Index = index;
            Rank = rank;
        }

        public static Result Make(out SaveDataAttribute attribute, ProgramId programId, SaveDataType type,
            UserId userId, ulong staticSaveDataId)
        {
            return Make(out attribute, programId, type, userId, staticSaveDataId, 0, SaveDataRank.Primary);
        }

        public static Result Make(out SaveDataAttribute attribute, ProgramId programId, SaveDataType type,
            UserId userId, ulong staticSaveDataId, ushort index)
        {
            return Make(out attribute, programId, type, userId, staticSaveDataId, index, SaveDataRank.Primary);
        }

        public static Result Make(out SaveDataAttribute attribute, ProgramId programId, SaveDataType type,
            UserId userId, ulong staticSaveDataId, ushort index, SaveDataRank rank)
        {
            UnsafeHelpers.SkipParamInit(out attribute);
            SaveDataAttribute tempAttribute = default;

            tempAttribute.ProgramId = programId;
            tempAttribute.Type = type;
            tempAttribute.UserId = userId;
            tempAttribute.StaticSaveDataId = staticSaveDataId;
            tempAttribute.Index = index;
            tempAttribute.Rank = rank;

            if (!SaveDataTypesValidity.IsValid(in tempAttribute))
                return ResultFs.InvalidArgument.Log();

            attribute = tempAttribute;
            return Result.Success;
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

    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    public struct SaveDataCreationInfo
    {
        [FieldOffset(0x00)] public long Size;
        [FieldOffset(0x08)] public long JournalSize;
        [FieldOffset(0x10)] public long BlockSize;
        [FieldOffset(0x18)] public ulong OwnerId;
        [FieldOffset(0x20)] public SaveDataFlags Flags;
        [FieldOffset(0x24)] public SaveDataSpaceId SpaceId;
        [FieldOffset(0x25)] public bool Field25;

        public static Result Make(out SaveDataCreationInfo creationInfo, long size, long journalSize, ulong ownerId,
            SaveDataFlags flags, SaveDataSpaceId spaceId)
        {
            UnsafeHelpers.SkipParamInit(out creationInfo);
            SaveDataCreationInfo tempCreationInfo = default;

            tempCreationInfo.Size = size;
            tempCreationInfo.JournalSize = journalSize;
            tempCreationInfo.BlockSize = SaveDataProperties.DefaultSaveDataBlockSize;
            tempCreationInfo.OwnerId = ownerId;
            tempCreationInfo.Flags = flags;
            tempCreationInfo.SpaceId = spaceId;
            tempCreationInfo.Field25 = false;

            if (!SaveDataTypesValidity.IsValid(in tempCreationInfo))
                return ResultFs.InvalidArgument.Log();

            creationInfo = tempCreationInfo;
            return Result.Success;
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

        [FieldOffset(0x08)] public SaveDataAttribute Attribute;

        public void SetProgramId(ProgramId value)
        {
            FilterByProgramId = true;
            Attribute.ProgramId = value;
        }

        public void SetSaveDataType(SaveDataType value)
        {
            FilterBySaveDataType = true;
            Attribute.Type = value;
        }

        public void SetUserId(UserId value)
        {
            FilterByUserId = true;
            Attribute.UserId = value;
        }

        public void SetSaveDataId(ulong value)
        {
            FilterBySaveDataId = true;
            Attribute.StaticSaveDataId = value;
        }

        public void SetIndex(ushort value)
        {
            FilterByIndex = true;
            Attribute.Index = value;
        }

        public static Result Make(out SaveDataFilter filter, Optional<ulong> programId, Optional<SaveDataType> saveType,
            Optional<UserId> userId, Optional<ulong> saveDataId, Optional<ushort> index)
        {
            return Make(out filter, programId, saveType, userId, saveDataId, index, SaveDataRank.Primary);
        }

        public static Result Make(out SaveDataFilter filter, Optional<ulong> programId, Optional<SaveDataType> saveType,
            Optional<UserId> userId, Optional<ulong> saveDataId, Optional<ushort> index, SaveDataRank rank)
        {
            UnsafeHelpers.SkipParamInit(out filter);

            SaveDataFilter tempFilter = Make(programId, saveType, userId, saveDataId, index, rank);

            if (!SaveDataTypesValidity.IsValid(in tempFilter))
                return ResultFs.InvalidArgument.Log();

            filter = tempFilter;
            return Result.Success;
        }

        public static SaveDataFilter Make(Optional<ulong> programId, Optional<SaveDataType> saveType,
            Optional<UserId> userId, Optional<ulong> saveDataId, Optional<ushort> index, SaveDataRank rank)
        {
            var filter = new SaveDataFilter();

            if (programId.HasValue)
            {
                filter.FilterByProgramId = true;
                filter.Attribute.ProgramId = new ProgramId(programId.Value);
            }

            if (saveType.HasValue)
            {
                filter.FilterBySaveDataType = true;
                filter.Attribute.Type = saveType.Value;
            }

            if (userId.HasValue)
            {
                filter.FilterByUserId = true;
                filter.Attribute.UserId = userId.Value;
            }

            if (saveDataId.HasValue)
            {
                filter.FilterBySaveDataId = true;
                filter.Attribute.StaticSaveDataId = saveDataId.Value;
            }

            if (index.HasValue)
            {
                filter.FilterByIndex = true;
                filter.Attribute.Index = index.Value;
            }

            filter.Rank = rank;

            return filter;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = HashLength)]
    public struct HashSalt
    {
        private const int HashLength = 0x20;

        [FieldOffset(0x00)] private byte _hashStart;

        public Span<byte> Hash => SpanHelpers.CreateSpan(ref _hashStart, HashLength);
        public ReadOnlySpan<byte> HashRo => SpanHelpers.CreateReadOnlySpan(in _hashStart, HashLength);
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public struct SaveDataMetaInfo
    {
        [FieldOffset(0)] public int Size;
        [FieldOffset(4)] public SaveDataMetaType Type;
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
        [FieldOffset(0x38)] public ushort Index;
        [FieldOffset(0x3A)] public SaveDataRank Rank;
        [FieldOffset(0x3B)] public SaveDataState State;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x200)]
    public struct SaveDataExtraData
    {
        [FieldOffset(0x00)] public SaveDataAttribute Attribute;
        [FieldOffset(0x40)] public ulong OwnerId;
        [FieldOffset(0x48)] public long TimeStamp;
        [FieldOffset(0x50)] public SaveDataFlags Flags;
        [FieldOffset(0x58)] public long DataSize;
        [FieldOffset(0x60)] public long JournalSize;
        [FieldOffset(0x68)] public long CommitId;
    }

    public struct CommitOption
    {
        public CommitOptionFlag Flags;
    }

    internal static class SaveDataTypesValidity
    {
        public static bool IsValid(in SaveDataAttribute attribute)
        {
            return IsValid(in attribute.Type) && IsValid(in attribute.Rank);
        }

        public static bool IsValid(in SaveDataCreationInfo creationInfo)
        {
            return creationInfo.Size >= 0 && creationInfo.JournalSize >= 0 && creationInfo.BlockSize >= 0 &&
                   IsValid(in creationInfo.SpaceId);
        }

        public static bool IsValid(in SaveDataMetaInfo metaInfo)
        {
            return IsValid(in metaInfo.Type);
        }

        public static bool IsValid(in SaveDataFilter filter)
        {
            return IsValid(in filter.Attribute);
        }

        public static bool IsValid(in SaveDataType type)
        {
            // SaveDataType.SystemBcat is excluded in this check
            return (uint)type <= (uint)SaveDataType.Cache;
        }

        public static bool IsValid(in SaveDataRank rank)
        {
            return (uint)rank <= (uint)SaveDataRank.Secondary;
        }

        public static bool IsValid(in SaveDataSpaceId spaceId)
        {
            return (uint)spaceId <= (uint)SaveDataSpaceId.SdCache || spaceId == SaveDataSpaceId.ProperSystem ||
                   spaceId == SaveDataSpaceId.SafeMode;
        }

        public static bool IsValid(in SaveDataMetaType metaType)
        {
            return (uint)metaType <= (uint)SaveDataMetaType.ExtensionContext;
        }
    }
}
