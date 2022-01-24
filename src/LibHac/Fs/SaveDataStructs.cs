using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.FsSrv.Impl;
using LibHac.Ncm;
using LibHac.Util;

namespace LibHac.Fs;

public struct SaveDataAttribute : IEquatable<SaveDataAttribute>, IComparable<SaveDataAttribute>
{
    public ProgramId ProgramId;
    public UserId UserId;
    public ulong StaticSaveDataId;
    public SaveDataType Type;
    public SaveDataRank Rank;
    public ushort Index;
    public Array24<byte> Reserved;

    public static Result Make(out SaveDataAttribute attribute, ProgramId programId, SaveDataType type,
        UserId userId, ulong staticSaveDataId)
    {
        return Make(out attribute, programId, type, userId, staticSaveDataId, index: 0, SaveDataRank.Primary);
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

    public readonly override bool Equals(object obj)
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

    public readonly override int GetHashCode()
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

public struct SaveDataCreationInfo
{
    public long Size;
    public long JournalSize;
    public long BlockSize;
    public ulong OwnerId;
    public SaveDataFlags Flags;
    public SaveDataSpaceId SpaceId;
    public bool IsPseudoSaveData;
    public Array26<byte> Reserved;

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
        tempCreationInfo.IsPseudoSaveData = false;

        if (!SaveDataTypesValidity.IsValid(in tempCreationInfo))
            return ResultFs.InvalidArgument.Log();

        creationInfo = tempCreationInfo;
        return Result.Success;
    }
}

public struct SaveDataFilter
{
    public bool FilterByProgramId;
    public bool FilterBySaveDataType;
    public bool FilterByUserId;
    public bool FilterBySaveDataId;
    public bool FilterByIndex;
    public SaveDataRank Rank;

    public SaveDataAttribute Attribute;

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
        Optional<UserId> userId, Optional<ulong> saveDataId, Optional<ushort> index)
    {
        return Make(programId, saveType, userId, saveDataId, index, SaveDataRank.Primary);
    }

    public static SaveDataFilter Make(Optional<ulong> programId, Optional<SaveDataType> saveType,
        Optional<UserId> userId, Optional<ulong> saveDataId, Optional<ushort> index, SaveDataRank rank)
    {
        SaveDataFilter filter = default;

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

public struct HashSalt
{
    private Array32<byte> _value;

    public Span<byte> Hash => _value.Items;
    public readonly ReadOnlySpan<byte> HashRo => _value.ItemsRo;
}

public struct SaveDataMetaInfo
{
    public int Size;
    public SaveDataMetaType Type;
    public Array11<byte> Reserved;
}

public struct SaveDataInfo
{
    public ulong SaveDataId;
    public SaveDataSpaceId SpaceId;
    public SaveDataType Type;
    public UserId UserId;
    public ulong StaticSaveDataId;
    public ProgramId ProgramId;
    public long Size;
    public ushort Index;
    public SaveDataRank Rank;
    public SaveDataState State;
    public Array36<byte> Reserved;
}

public struct SaveDataExtraData
{
    public SaveDataAttribute Attribute;
    public ulong OwnerId;
    public long TimeStamp;
    public SaveDataFlags Flags;
    public long DataSize;
    public long JournalSize;
    public long CommitId;
    public Array400<byte> Reserved;
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
        return (uint)spaceId <= (uint)SaveDataSpaceId.SdUser || spaceId == SaveDataSpaceId.ProperSystem ||
               spaceId == SaveDataSpaceId.SafeMode;
    }

    public static bool IsValid(in SaveDataMetaType metaType)
    {
        return (uint)metaType <= (uint)SaveDataMetaType.ExtensionContext;
    }
}