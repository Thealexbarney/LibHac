using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Util;
using static LibHac.Fs.Impl.AccessLogStrings;
using static LibHac.Fs.SaveData;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for mounting save data and checking if save data already exists or not.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
[SkipLocalsInit]
public static class SaveData
{
    private const long SaveDataTotalSizeMax = 0xFA000000;
    private const int SaveDataBlockSize = 0x4000;

    private static Result OpenSaveDataInternalStorageFileSystemImpl(FileSystemClient fs,
        ref UniqueRef<IFileSystem> outFileSystem, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
        using var fileSystem = new SharedRef<IFileSystemSf>();

        Result res = fileSystemProxy.Get.OpenSaveDataInternalStorageFileSystem(ref fileSystem.Ref, spaceId,
            saveDataId);
        if (res.IsFailure()) return res.Miss();

        using var fileSystemAdapter = new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(in fileSystem));

        outFileSystem.Set(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    private static Result ExtendSaveDataIfNeeded(FileSystemClient fs, UserId userId, long saveDataSize,
        long saveDataJournalSize)
    {
        // Find the save data for the current program.
        Result res = SaveDataFilter.Make(out SaveDataFilter filter, InvalidProgramId.Value, SaveDataType.Account, userId,
            InvalidSystemSaveDataId, index: 0, SaveDataRank.Primary);
        if (res.IsFailure()) return res.Miss();

        res = fs.Impl.FindSaveDataWithFilter(out SaveDataInfo info, SaveDataSpaceId.User, in filter);
        if (res.IsFailure()) return res.Miss();

        SaveDataSpaceId spaceId = info.SpaceId;
        ulong saveDataId = info.SaveDataId;

        // Get the current save data's sizes.
        res = fs.Impl.GetSaveDataAvailableSize(out long availableSize, spaceId, saveDataId);
        if (res.IsFailure()) return res.Miss();

        res = fs.Impl.GetSaveDataJournalSize(out long journalSize, spaceId, saveDataId);
        if (res.IsFailure()) return res.Miss();

        // Extend the save data if it's not large enough.
        if (availableSize < saveDataSize || journalSize < saveDataJournalSize)
        {
            long newSaveDataSize = Math.Max(saveDataSize, availableSize);
            long newJournalSize = Math.Max(saveDataJournalSize, journalSize);
            res = fs.Impl.ExtendSaveData(spaceId, saveDataId, newSaveDataSize, newJournalSize);
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    private static Result MountSaveDataImpl(this FileSystemClientImpl fs, U8Span mountName, SaveDataSpaceId spaceId,
        ProgramId programId, UserId userId, SaveDataType type, bool openReadOnly, ushort index)
    {
        Result res = fs.CheckMountName(mountName);
        if (res.IsFailure()) return res.Miss();

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

        res = SaveDataAttribute.Make(out SaveDataAttribute attribute, programId, type, userId, InvalidSystemSaveDataId,
            index);
        if (res.IsFailure()) return res.Miss();

        using var fileSystem = new SharedRef<IFileSystemSf>();

        if (openReadOnly)
        {
            res = fileSystemProxy.Get.OpenReadOnlySaveDataFileSystem(ref fileSystem.Ref, spaceId, in attribute);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            res = fileSystemProxy.Get.OpenSaveDataFileSystem(ref fileSystem.Ref, spaceId, in attribute);
            if (res.IsFailure()) return res.Miss();
        }

        // Note: Nintendo does pass in the same object both as a unique_ptr and as a raw pointer.
        // Both of these are tied to the lifetime of the created FileSystemServiceObjectAdapter so it shouldn't be an issue.
        var fileSystemAdapterRaw = new FileSystemServiceObjectAdapter(in fileSystem);
        using var fileSystemAdapter = new UniqueRef<IFileSystem>(fileSystemAdapterRaw);

        if (!fileSystemAdapter.HasValue)
            return ResultFs.AllocationMemoryFailedNew.Log();

        using var mountNameGenerator = new UniqueRef<ICommonMountNameGenerator>();

        res = fs.Fs.Register(mountName, fileSystemAdapterRaw, ref fileSystemAdapter.Ref, ref mountNameGenerator.Ref,
            false, null, true);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result EnsureSaveDataImpl(this FileSystemClientImpl fs, UserId userId, long saveDataSize,
        long saveDataJournalSize, bool extendIfNeeded)
    {
        if (!Alignment.IsAligned(saveDataSize, SaveDataBlockSize))
            return ResultFs.InvalidSize.Log();

        if (!Alignment.IsAligned(saveDataJournalSize, SaveDataBlockSize))
            return ResultFs.InvalidSize.Log();

        if (saveDataSize + saveDataJournalSize > SaveDataTotalSizeMax)
            return ResultFs.InvalidSize.Log();

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

        Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, InvalidProgramId, SaveDataType.Account,
            userId, InvalidSystemSaveDataId);
        if (res.IsFailure()) return res.Miss();

        res = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, saveDataSize, saveDataJournalSize,
            ownerId: 0, SaveDataFlags.None, SaveDataSpaceId.User);
        if (res.IsFailure()) return res.Miss();

        var metaInfo = new SaveDataMetaInfo
        {
            Type = SaveDataMetaType.None,
            Size = 0
        };

        res = fileSystemProxy.Get.CreateSaveDataFileSystem(in attribute, in creationInfo, in metaInfo);

        if (res.IsFailure())
        {
            // Ensure the save is large enough if it already exists
            if (ResultFs.PathAlreadyExists.Includes(res))
            {
                if (extendIfNeeded)
                {
                    res = ExtendSaveDataIfNeeded(fs.Fs, userId, saveDataSize, saveDataJournalSize);
                    if (res.IsFailure()) return res.Miss();
                }
            }
            else
            {
                return res.Miss();
            }
        }

        return Result.Success;
    }

    public static Result MountSaveDataImpl(this FileSystemClientImpl fs, U8Span mountName, UserId userId)
    {
        return MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, ProgramId.InvalidId, userId,
            SaveDataType.Account, openReadOnly: false, index: 0);
    }

    public static Result MountSaveData(this FileSystemClient fs, U8Span mountName, UserId userId)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x60];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = MountSaveDataImpl(fs.Impl, mountName, InvalidUserId);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16);

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = MountSaveDataImpl(fs.Impl, mountName, InvalidUserId);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static Result MountSaveData(this FileSystemClient fs, U8Span mountName, Ncm.ApplicationId applicationId,
        UserId userId)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x90];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId, userId,
                SaveDataType.Account, openReadOnly: false, index: 0);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16);

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId, userId,
                SaveDataType.Account, openReadOnly: false, index: 0);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static Result MountSaveDataReadOnly(this FileSystemClient fs, U8Span mountName,
        Ncm.ApplicationId applicationId, UserId userId)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x90];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId, userId,
                SaveDataType.Account, openReadOnly: true, index: 0);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16);

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId, userId,
                SaveDataType.Account, openReadOnly: true, index: 0);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static Result IsSaveDataExisting(this FileSystemClientImpl fs, out bool exists, UserId userId)
    {
        return IsSaveDataExisting(fs, out exists, default, userId);
    }

    public static Result IsSaveDataExisting(this FileSystemClientImpl fs, out bool exists,
        Ncm.ApplicationId applicationId, UserId userId)
    {
        return IsSaveDataExisting(fs, out exists, default, SaveDataType.Account, userId);
    }

    public static Result IsSaveDataExisting(this FileSystemClientImpl fs, out bool exists,
        Ncm.ApplicationId applicationId, SaveDataType type, UserId userId)
    {
        UnsafeHelpers.SkipParamInit(out exists);

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

        Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, type, userId,
            InvalidSystemSaveDataId);
        if (res.IsFailure()) return res.Miss();

        using var fileSystem = new SharedRef<IFileSystemSf>();
        res = fileSystemProxy.Get.OpenSaveDataFileSystem(ref fileSystem.Ref, SaveDataSpaceId.User, in attribute);

        if (res.IsSuccess() || ResultFs.TargetLocked.Includes(res) || ResultFs.SaveDataExtending.Includes(res))
        {
            exists = true;
            return Result.Success;
        }

        if (ResultFs.TargetNotFound.Includes(res))
        {
            exists = false;
            return Result.Success;
        }

        return res.Miss();
    }

    public static Result CleanUpTemporaryStorageImpl(this FileSystemClientImpl fs)
    {
        while (true)
        {
            Result res = SaveDataFilter.Make(out SaveDataFilter filter, programId: default, SaveDataType.Temporary,
                userId: default, saveDataId: default, index: default);

            fs.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            // Try to find any temporary save data.
            res = fs.FindSaveDataWithFilter(out SaveDataInfo info, SaveDataSpaceId.Temporary, in filter);

            if (res.IsFailure())
            {
                if (ResultFs.TargetNotFound.Includes(res))
                {
                    // No more save data found. We're done cleaning.
                    return Result.Success;
                }

                fs.AbortIfNeeded(res);
                return res.Miss();
            }

            // Delete the found save data.
            res = fs.DeleteSaveData(SaveDataSpaceId.Temporary, info.SaveDataId);
            fs.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();
        }
    }

    public static Result MountTemporaryStorage(this FileSystemClient fs, U8Span mountName)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x30];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.Temporary, InvalidProgramId, InvalidUserId,
                SaveDataType.Temporary, openReadOnly: false, index: 0);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote);

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.Temporary, InvalidProgramId, InvalidUserId,
                SaveDataType.Temporary, openReadOnly: false, index: 0);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static Result MountCacheStorage(this FileSystemClient fs, U8Span mountName)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x30];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, InvalidProgramId,
                InvalidUserId, SaveDataType.Cache, openReadOnly: false, index: 0);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote);

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, InvalidProgramId,
                InvalidUserId, SaveDataType.Cache, openReadOnly: false, index: 0);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static Result MountCacheStorage(this FileSystemClient fs, U8Span mountName, int index)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x40];


        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, InvalidProgramId,
                InvalidUserId, SaveDataType.Cache, openReadOnly: false, (ushort)index);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogIndex).AppendFormat(index);

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, InvalidProgramId,
                InvalidUserId, SaveDataType.Cache, openReadOnly: false, (ushort)index);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static Result MountCacheStorage(this FileSystemClient fs, U8Span mountName, Ncm.ApplicationId applicationId)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x50];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId,
                InvalidUserId, SaveDataType.Cache, openReadOnly: false, index: 0);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogApplicationId).AppendFormat(applicationId.Value, 'X');

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId,
                InvalidUserId, SaveDataType.Cache, openReadOnly: false, index: 0);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static Result MountCacheStorage(this FileSystemClient fs, U8Span mountName, Ncm.ApplicationId applicationId,
        int index)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x60];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId,
                InvalidUserId, SaveDataType.Cache, openReadOnly: false, (ushort)index);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                .Append(LogIndex).AppendFormat(index);

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId,
                InvalidUserId, SaveDataType.Cache, openReadOnly: false, (ushort)index);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static Result OpenSaveDataInternalStorageFileSystem(this FileSystemClient fs,
        ref UniqueRef<IFileSystem> outFileSystem, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Result res = OpenSaveDataInternalStorageFileSystemImpl(fs, ref outFileSystem, spaceId, saveDataId);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result MountSaveDataInternalStorage(this FileSystemClient fs, U8Span mountName,
        SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Result res = Operate(fs, mountName, spaceId, saveDataId);

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;

        static Result Operate(FileSystemClient fs, U8Span mountName, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result res = fs.Impl.CheckMountName(mountName);
            if (res.IsFailure()) return res.Miss();

            using var fileSystem = new UniqueRef<IFileSystem>();
            res = OpenSaveDataInternalStorageFileSystemImpl(fs, ref fileSystem.Ref, spaceId, saveDataId);
            if (res.IsFailure()) return res.Miss();

            return fs.Register(mountName, ref fileSystem.Ref);
        }
    }
}