using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;

using static LibHac.Fs.Impl.AccessLogStrings;
using static LibHac.Fs.SaveData;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for mounting system save data file systems.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
[SkipLocalsInit]
public static class SystemSaveData
{
    public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName, ulong saveDataId)
    {
        return fs.MountSystemSaveData(mountName, saveDataId, InvalidUserId);
    }

    public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName, ulong saveDataId,
        UserId userId)
    {
        return fs.MountSystemSaveData(mountName, SaveDataSpaceId.System, saveDataId, userId);
    }

    public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        return fs.MountSystemSaveData(mountName, spaceId, saveDataId, InvalidUserId);
    }

    public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName,
        SaveDataSpaceId spaceId, ulong saveDataId, UserId userId)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x90];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Mount(fs, mountName, spaceId, saveDataId, userId);
            Tick end = fs.Hos.Os.GetSystemTick();

            var idString = new IdString();
            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName)
                .Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                .Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16);

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = Mount(fs, mountName, spaceId, saveDataId, userId);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return res;

        static Result Mount(FileSystemClient fs, U8Span mountName, SaveDataSpaceId spaceId, ulong saveDataId,
            UserId userId)
        {
            Result res = fs.Impl.CheckMountName(mountName);
            if (res.IsFailure()) return res.Miss();

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            res = SaveDataAttribute.Make(out SaveDataAttribute attribute, InvalidProgramId,
                SaveDataType.System, userId, saveDataId);
            if (res.IsFailure()) return res.Miss();

            using var fileSystem = new SharedRef<IFileSystemSf>();

            res = fileSystemProxy.Get.OpenSaveDataFileSystemBySystemSaveDataId(ref fileSystem.Ref(), spaceId, in attribute);
            if (res.IsFailure()) return res.Miss();

            var fileSystemAdapterRaw = new FileSystemServiceObjectAdapter(ref fileSystem.Ref());
            using var fileSystemAdapter = new UniqueRef<IFileSystem>(fileSystemAdapterRaw);

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInSystemSaveDataA.Log();

            if (spaceId == SaveDataSpaceId.System)
            {
                using var mountNameGenerator = new UniqueRef<ICommonMountNameGenerator>();
                return fs.Register(mountName, multiCommitTarget: fileSystemAdapterRaw, ref fileSystemAdapter.Ref(),
                    ref mountNameGenerator.Ref(), useDataCache: false, storageForPurgeFileDataCache: null,
                    usePathCache: false);
            }
            else
            {
                return fs.Register(mountName, ref fileSystemAdapter.Ref());
            }
        }
    }
}