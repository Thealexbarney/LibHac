using System;
using LibHac.Account;
using LibHac.Common;

namespace LibHac.Fs;

public static class UserAccountSystemSaveData
{
    public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName, ulong saveDataId, in Uid user)
    {
        throw new NotImplementedException();
    }

    public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, in Uid user, long size,
        long journalSize, SaveDataFlags flags)
    {
        throw new NotImplementedException();
    }

    public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, in Uid user, ulong ownerId,
        long size, long journalSize, SaveDataFlags flags, SaveDataFormatType formatType)
    {
        throw new NotImplementedException();
    }

    public static Result DeleteSystemSaveData(this FileSystemClient fs, ulong saveDataId, in Uid user)
    {
        throw new NotImplementedException();
    }

    public static Result GetSystemSaveDataFlags(this FileSystemClient fs, out SaveDataFlags outFlags, ulong saveDataId,
        in Uid user)
    {
        throw new NotImplementedException();
    }

    public static Result SetSystemSaveDataFlags(this FileSystemClient fs, ulong saveDataId, in Uid user,
        SaveDataFlags flags)
    {
        throw new NotImplementedException();
    }
}