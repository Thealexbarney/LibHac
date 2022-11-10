using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;
using static LibHac.Fs.SaveData;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains save data functions used for debugging during development.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
[SkipLocalsInit]
public static class SaveDataForDebug
{
    private const long SaveDataSizeForDebug = 0x2000000;
    private const long SaveDataJournalSizeForDebug = 0x2000000;

    public static void SetSaveDataRootPath(this FileSystemClient fs, U8Span path)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x300];

        if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = SetRootPath(fs, path);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogPath).Append(path).Append(LogQuote);

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = SetRootPath(fs, path);
        }

        fs.Impl.LogResultErrorMessage(res);
        Abort.DoAbortUnlessSuccess(res);

        static Result SetRootPath(FileSystemClient fs, U8Span path)
        {
            Result res = PathUtility.ConvertToFspPath(out FspPath sfPath, path);
            if (res.IsFailure()) return res.Miss();

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            res = fileSystemProxy.Get.SetSaveDataRootPath(in sfPath);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }

    public static void UnsetSaveDataRootPath(this FileSystemClient fs)
    {
        Result res;

        if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            res = fileSystemProxy.Get.UnsetSaveDataRootPath();
            Tick end = fs.Hos.Os.GetSystemTick();

            fs.Impl.OutputAccessLog(res, start, end, null, U8Span.Empty);
        }
        else
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            res = fileSystemProxy.Get.UnsetSaveDataRootPath();
        }

        fs.Impl.LogResultErrorMessage(res);
        Abort.DoAbortUnlessSuccess(res);
    }

    /// <summary>
    /// Ensures the current application's debug save data is at least as large as the specified values.
    /// </summary>
    /// <remarks>Each application can have a single debug save. This save data is not associated with any
    /// user account and is intended for debug use when developing an application.</remarks>
    /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
    /// <param name="saveDataSize">The size of the usable space in the save data.</param>
    /// <param name="saveDataJournalSize">The size of the save data's journal.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.TargetNotFound"/>: The save data was not found.<br/>
    /// <see cref="ResultFs.TargetLocked"/>: The save data is currently open or otherwise in use.<br/>
    /// <see cref="ResultFs.UsableSpaceNotEnough"/>: Insufficient free space to create or extend the save data.<br/>
    /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.</returns>
    public static Result EnsureSaveDataForDebug(this FileSystemClient fs, long saveDataSize, long saveDataJournalSize)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x60];

        if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Ensure(fs, saveDataSize, saveDataJournalSize);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogSaveDataSize).AppendFormat(saveDataSize, 'd')
                .Append(LogSaveDataJournalSize).AppendFormat(saveDataJournalSize, 'd');

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = Ensure(fs, saveDataSize, saveDataJournalSize);
        }

        if (res.IsFailure()) return res.Miss();

        return Result.Success;

        static Result Ensure(FileSystemClient fs, long saveDataSize, long saveDataJournalSize)
        {
            UserId userIdForDebug = InvalidUserId;

            Result res = fs.Impl.EnsureSaveDataImpl(userIdForDebug, saveDataSize, saveDataJournalSize,
                extendIfNeeded: true);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }

    /// <summary>
    /// Mounts the debug save data for the current application. Each application can have its own debug save
    /// that is not associated with any user account.
    /// </summary>
    /// <remarks>Each application can have a single debug save. This save data is not associated with any
    /// user account and is intended for debug use when developing an application.</remarks>
    /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
    /// <param name="mountName">The mount name at which the file system will be mounted.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.TargetNotFound"/>: The save data was not found.<br/>
    /// <see cref="ResultFs.TargetLocked"/>: The save data is currently open or otherwise in use.<br/>
    /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.</returns>
    public static Result MountSaveDataForDebug(this FileSystemClient fs, U8Span mountName)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x30];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Mount(fs, mountName);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote);

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = Mount(fs, mountName);
        }

        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result Mount(FileSystemClient fs, U8Span mountName)
        {
            UserId userIdForDebug = InvalidUserId;

            Result res = fs.Impl.EnsureSaveDataImpl(userIdForDebug, SaveDataSizeForDebug, SaveDataJournalSizeForDebug,
                extendIfNeeded: false);
            if (res.IsFailure()) return res.Miss();

            res = fs.Impl.MountSaveDataImpl(mountName, userIdForDebug);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }
}