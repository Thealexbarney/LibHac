// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Account;
using LibHac.Common;
using LibHac.Ncm;

namespace LibHac.Fs;

file static class Anonymous
{
    public static Result EnsureSaveDataImpl(FileSystemClient fs, out long outRequiredSize, in Uid user)
    {
        throw new NotImplementedException();
    }

    public static Result CreateCacheStorageImpl(FileSystemClient fs, out long outRequiredSize,
        out CacheStorageTargetMedia outCacheStorageTargetMedia, ushort index, long cacheStorageSize,
        long cacheStorageJournalSize)
    {
        throw new NotImplementedException();
    }

    public static Result ExtendSaveDataImpl(FileSystemClient fs, out long outRequiredSize, SaveDataType saveDataType,
        in Uid user, long saveDataSize, long saveDataJournalSize)
    {
        throw new NotImplementedException();
    }

    public static Result GetSaveDataSizeImpl(FileSystemClient fs, out long outSaveDataSize,
        out long saveDataJournalSize, SaveDataType saveDataType, in Uid user)
    {
        throw new NotImplementedException();
    }

    public static Result ShowLibraryAppletDataErase(FileSystemClient fs, in Uid user,
        CacheStorageTargetMedia cacheStorageTargetMedia, long requiredSize, Result errorResult,
        SaveDataType saveDataType)
    {
        throw new NotImplementedException();
    }
}

public static class SaveData
{
    public static readonly ulong SaveIndexerId = 0x8000000000000000;
    public static ProgramId InvalidProgramId => default;
    public static ProgramId AutoResolveCallerProgramId => new ProgramId(ulong.MaxValue - 1);
    public static UserId InvalidUserId => default;
    public static ulong InvalidSystemSaveDataId => 0;

    public static Result MountSaveData(this FileSystemClient fs, U8Span mountName, in Uid user)
    {
        throw new NotImplementedException();
    }

    public static Result MountSaveData(this FileSystemClient fs, U8Span mountName, ApplicationId applicationId,
        in Uid user)
    {
        throw new NotImplementedException();
    }

    public static Result MountSaveDataReadOnly(this FileSystemClient fs, U8Span mountName, ApplicationId applicationId,
        in Uid user)
    {
        throw new NotImplementedException();
    }

    public static bool IsSaveDataExisting(this FileSystemClient fs, in Uid user)
    {
        throw new NotImplementedException();
    }

    public static bool IsSaveDataExisting(this FileSystemClient fs, ApplicationId applicationId, in Uid user)
    {
        throw new NotImplementedException();
    }

    public static Result IsSaveDataExisting(this FileSystemClient fs, out bool outIsExisting, in Uid user)
    {
        throw new NotImplementedException();
    }

    public static Result IsSaveDataExisting(this FileSystemClient fs, ApplicationId applicationId,
        out bool outIsExisting, in Uid user)
    {
        throw new NotImplementedException();
    }

    public static Result EnsureSaveData(this FileSystemClient fs, in Uid user)
    {
        throw new NotImplementedException();
    }

    public static Result EnsureDeviceSaveDataUnsafe(this FileSystemClient fs, ApplicationId applicationId,
        long saveDataSize, long saveDataJournalSize)
    {
        throw new NotImplementedException();
    }

    public static void GetSaveDataSizeMax(this FileSystemClient fs, out long outSaveDataSizeMax,
        out long outSaveDataJournalSizeMax)
    {
        throw new NotImplementedException();
    }

    public static void GetDeviceSaveDataSizeMax(this FileSystemClient fs, out long outSaveDataSizeMax,
        out long outSaveDataJournalSizeMax)
    {
        throw new NotImplementedException();
    }

    public static Result ExtendSaveData(this FileSystemClient fs, in Uid user, long saveDataSize,
        long saveDataJournalSize)
    {
        throw new NotImplementedException();
    }

    public static Result ExtendDeviceSaveData(this FileSystemClient fs, long saveDataSize, long saveDataJournalSize)
    {
        throw new NotImplementedException();
    }

    public static Result GetSaveDataSize(this FileSystemClient fs, out long outSaveDataSize,
        out long outSaveDataJournalSize, in Uid user)
    {
        throw new NotImplementedException();
    }

    public static Result GetDeviceSaveDataSize(this FileSystemClient fs, out long outSaveDataSize,
        out long outSaveDataJournalSize)
    {
        throw new NotImplementedException();
    }

    public static void GetCacheStorageMax(this FileSystemClient fs, out int outCacheStorageIndexMax,
        out long outCacheStorageDataAndJournalSizeMax)
    {
        throw new NotImplementedException();
    }

    public static Result CreateCacheStorage(this FileSystemClient fs, int index, long cacheStorageSize,
        long cacheStorageJournalSize)
    {
        throw new NotImplementedException();
    }
}