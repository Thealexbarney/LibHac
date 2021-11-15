﻿using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim;

[SkipLocalsInit]
public static class SaveData
{
    private static Result MountSaveDataImpl(this FileSystemClientImpl fs, U8Span mountName, SaveDataSpaceId spaceId,
        ProgramId programId, UserId userId, SaveDataType type, bool openReadOnly, ushort index)
    {
        Result rc = fs.CheckMountName(mountName);
        if (rc.IsFailure()) return rc;

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

        rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, programId, type, userId, 0, index);
        if (rc.IsFailure()) return rc;

        using var fileSystem = new SharedRef<IFileSystemSf>();

        if (openReadOnly)
        {
            rc = fileSystemProxy.Get.OpenReadOnlySaveDataFileSystem(ref fileSystem.Ref(), spaceId, in attribute);
            if (rc.IsFailure()) return rc;
        }
        else
        {
            rc = fileSystemProxy.Get.OpenSaveDataFileSystem(ref fileSystem.Ref(), spaceId, in attribute);
            if (rc.IsFailure()) return rc;
        }

        // Note: Nintendo does pass in the same object both as a unique_ptr and as a raw pointer.
        // Both of these are tied to the lifetime of the created FileSystemServiceObjectAdapter so it shouldn't be an issue.
        var fileSystemAdapterRaw = new FileSystemServiceObjectAdapter(ref fileSystem.Ref());
        using var fileSystemAdapter = new UniqueRef<IFileSystem>(fileSystemAdapterRaw);

        if (!fileSystemAdapter.HasValue)
            return ResultFs.AllocationMemoryFailedNew.Log();

        using var mountNameGenerator = new UniqueRef<ICommonMountNameGenerator>();

        rc = fs.Fs.Register(mountName, fileSystemAdapterRaw, ref fileSystemAdapter.Ref(),
            ref mountNameGenerator.Ref(), false, true);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result MountSaveData(this FileSystemClient fs, U8Span mountName, Ncm.ApplicationId applicationId,
        UserId userId)
    {
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x90];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId, userId,
                SaveDataType.Account, openReadOnly: false, index: 0);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16);

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId, userId,
                SaveDataType.Account, openReadOnly: false, index: 0);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return rc;
    }

    public static Result MountSaveDataReadOnly(this FileSystemClient fs, U8Span mountName,
        Ncm.ApplicationId applicationId, UserId userId)
    {
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x90];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId, userId,
                SaveDataType.Account, openReadOnly: true, index: 0);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16);

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId, userId,
                SaveDataType.Account, openReadOnly: true, index: 0);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return rc;
    }

    public static Result MountTemporaryStorage(this FileSystemClient fs, U8Span mountName)
    {
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x30];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.Temporary, Fs.SaveData.InvalidProgramId,
                Fs.SaveData.InvalidUserId, SaveDataType.Temporary, openReadOnly: false, index: 0);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote);

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.Temporary, Fs.SaveData.InvalidProgramId,
                Fs.SaveData.InvalidUserId, SaveDataType.Temporary, openReadOnly: false, index: 0);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return rc;
    }

    public static Result MountCacheStorage(this FileSystemClient fs, U8Span mountName)
    {
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x30];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, Fs.SaveData.InvalidProgramId,
                Fs.SaveData.InvalidUserId, SaveDataType.Cache, openReadOnly: false, index: 0);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote);

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, Fs.SaveData.InvalidProgramId,
                Fs.SaveData.InvalidUserId, SaveDataType.Cache, openReadOnly: false, index: 0);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return rc;
    }

    public static Result MountCacheStorage(this FileSystemClient fs, U8Span mountName, int index)
    {
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x40];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, Fs.SaveData.InvalidProgramId,
                Fs.SaveData.InvalidUserId, SaveDataType.Cache, openReadOnly: false, (ushort)index);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogIndex).AppendFormat(index);

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, Fs.SaveData.InvalidProgramId,
                Fs.SaveData.InvalidUserId, SaveDataType.Cache, openReadOnly: false, (ushort)index);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return rc;
    }

    public static Result MountCacheStorage(this FileSystemClient fs, U8Span mountName,
        Ncm.ApplicationId applicationId)
    {
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x50];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId,
                Fs.SaveData.InvalidUserId, SaveDataType.Cache, openReadOnly: false, index: 0);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogApplicationId).AppendFormat(applicationId.Value, 'X');

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId,
                Fs.SaveData.InvalidUserId, SaveDataType.Cache, openReadOnly: false, index: 0);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return rc;
    }

    public static Result MountCacheStorage(this FileSystemClient fs, U8Span mountName,
        Ncm.ApplicationId applicationId, int index)
    {
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x60];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId,
                Fs.SaveData.InvalidUserId, SaveDataType.Cache, openReadOnly: false, (ushort)index);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                .Append(LogIndex).AppendFormat(index);

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = MountSaveDataImpl(fs.Impl, mountName, SaveDataSpaceId.User, applicationId,
                Fs.SaveData.InvalidUserId, SaveDataType.Cache, openReadOnly: false, (ushort)index);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return rc;
    }
}
