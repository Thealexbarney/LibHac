using System;
using System.Diagnostics.CodeAnalysis;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.FsSrv.Impl;
using LibHac.Ncm;
using LibHac.Util;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

public enum SaveDataSpaceId : byte
{
    System = 0,
    User = 1,
    SdSystem = 2,
    Temporary = 3,
    SdUser = 4,
    ProperSystem = 100,
    SafeMode = 101
}

public enum SaveDataType : byte
{
    System = 0,
    Account = 1,
    Bcat = 2,
    Device = 3,
    Temporary = 4,
    Cache = 5,
    SystemBcat = 6
}

public enum SaveDataRank : byte
{
    Primary = 0,
    Secondary = 1
}

public enum SaveDataFormatType : byte
{
    Normal = 0,
    NoJournal = 1
}

[Flags]
public enum SaveDataFlags
{
    None = 0,
    KeepAfterResettingSystemSaveData = 1 << 0,
    KeepAfterRefurbishment = 1 << 1,
    KeepAfterResettingSystemSaveDataWithoutUserSaveData = 1 << 2,
    NeedsSecureDelete = 1 << 3,
    Restore = 1 << 4
}

public enum SaveDataState : byte
{
    Normal = 0,
    Processing = 1,
    State2 = 2,
    MarkedForDeletion = 3,
    Extending = 4,
    ImportSuspended = 5
}

public struct SaveDataMetaInfo
{
    public int Size;
    public SaveDataMetaType Type;
    public Array11<byte> Reserved;
}

public enum SaveDataMetaType : byte
{
    None = 0,
    Thumbnail = 1,
    ExtensionContext = 2
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
    public SaveDataFormatType FormatType;
    public long DataSize;
    public long JournalSize;
    public long CommitId;
    public Array400<byte> Reserved;
}

public struct CommitOption
{
    public CommitOptionFlag Flags;
}

[Flags]
public enum CommitOptionFlag
{
    None = 0,
    ClearRestoreFlag = 1,
    SetRestoreFlag = 2
}

public struct HashSalt
{
    private Array32<byte> _value;

    [UnscopedRef] public Span<byte> Hash => _value;
    [UnscopedRef] public readonly ReadOnlySpan<byte> HashRo => _value;
}

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
               Index == other.Index &&
               Rank == other.Rank;
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
        int indexComparison = Index.CompareTo(other.Index);
        if (indexComparison != 0) return indexComparison;
        return ((int)Rank).CompareTo((int)other.Rank);
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

public struct SaveDataCreationInfo2
{
    internal const uint SaveDataCreationInfo2Version = 0x00010000;

    public uint Version;
    public SaveDataAttribute Attribute;
    public long Size;
    public long JournalSize;
    public long BlockSize;
    public ulong OwnerId;
    public SaveDataFlags Flags;
    public SaveDataSpaceId SpaceId;
    public SaveDataFormatType FormatType;
    public Array2<byte> Reserved1;
    public bool IsHashSaltEnabled;
    public Array3<byte> Reserved2;
    public HashSalt HashSalt;
    public SaveDataMetaType MetaType;
    public Array3<byte> Reserved3;
    public int MetaSize;
    public Array356<byte> Reserved4;

    public static Result Make(out SaveDataCreationInfo2 creationInfo, in SaveDataAttribute attribute, long size,
        long journalSize, long blockSize, ulong ownerId, SaveDataFlags flags, SaveDataSpaceId spaceId,
        SaveDataFormatType formatType)
    {
        UnsafeHelpers.SkipParamInit(out creationInfo);
        SaveDataCreationInfo2 tempCreationInfo = default;

        tempCreationInfo.Version = SaveDataCreationInfo2Version;
        tempCreationInfo.Attribute = attribute;
        tempCreationInfo.Size = size;
        tempCreationInfo.JournalSize = journalSize;
        tempCreationInfo.BlockSize = blockSize;
        tempCreationInfo.OwnerId = ownerId;
        tempCreationInfo.Flags = flags;
        tempCreationInfo.SpaceId = spaceId;
        tempCreationInfo.FormatType = formatType;

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

internal static class SaveDataTypesValidity
{
    public static bool IsValid(in SaveDataAttribute attribute)
    {
        return IsValid(in attribute.Type) && IsValid(in attribute.Rank);
    }

    public static bool IsValid(in SaveDataCreationInfo creationInfo)
    {
        return creationInfo.Size >= 0
               && creationInfo.JournalSize >= 0
               && creationInfo.BlockSize >= 0
               && IsValid(in creationInfo.SpaceId);
    }

    public static bool IsValid(in SaveDataCreationInfo2 creationInfo)
    {
        foreach (byte b in creationInfo.Reserved1)
            if (b != 0) return false;

        foreach (byte b in creationInfo.Reserved2)
            if (b != 0) return false;

        foreach (byte b in creationInfo.Reserved3)
            if (b != 0) return false;

        foreach (byte b in creationInfo.Reserved4)
            if (b != 0) return false;

        foreach (byte b in creationInfo.Attribute.Reserved)
            if (b != 0) return false;

        return IsValid(in creationInfo.Attribute)
               && creationInfo.Size >= 0
               && creationInfo.JournalSize >= 0
               && creationInfo.BlockSize >= 0
               && IsValid(in creationInfo.SpaceId)
               && IsValid(in creationInfo.FormatType)
               && IsValid(in creationInfo.MetaType);
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

    public static bool IsValid(in SaveDataFormatType type)
    {
        return (uint)type <= (uint)SaveDataFormatType.NoJournal;
    }

    public static bool IsValid(in SaveDataRank rank)
    {
        return (uint)rank <= (uint)SaveDataRank.Secondary;
    }

    public static bool IsValid(in SaveDataSpaceId spaceId)
    {
        return (uint)spaceId <= (uint)SaveDataSpaceId.SdUser
               || spaceId == SaveDataSpaceId.ProperSystem
               || spaceId == SaveDataSpaceId.SafeMode;
    }

    public static bool IsValid(in SaveDataMetaType metaType)
    {
        return (uint)metaType <= (uint)SaveDataMetaType.ExtensionContext;
    }
}