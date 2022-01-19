using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for mounting system save data file systems.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0</remarks>
[SkipLocalsInit]
public static class SystemSaveData
{
    public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName, ulong saveDataId)
    {
        return fs.MountSystemSaveData(mountName, saveDataId, Fs.SaveData.InvalidUserId);
    }

    public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName, ulong saveDataId,
        UserId userId)
    {
        return fs.MountSystemSaveData(mountName, SaveDataSpaceId.System, saveDataId, userId);
    }

    public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        return fs.MountSystemSaveData(mountName, spaceId, saveDataId, Fs.SaveData.InvalidUserId);
    }

    public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName,
        SaveDataSpaceId spaceId, ulong saveDataId, UserId userId)
    {
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x90];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = Mount(fs, mountName, spaceId, saveDataId, userId);
            Tick end = fs.Hos.Os.GetSystemTick();

            var idString = new IdString();
            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName)
                .Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                .Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16);

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = Mount(fs, mountName, spaceId, saveDataId, userId);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return rc;

        static Result Mount(FileSystemClient fs, U8Span mountName, SaveDataSpaceId spaceId, ulong saveDataId,
            UserId userId)
        {
            Result rc = fs.Impl.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, Fs.SaveData.InvalidProgramId,
                SaveDataType.System, userId, saveDataId);
            if (rc.IsFailure()) return rc;

            using var fileSystem = new SharedRef<IFileSystemSf>();

            rc = fileSystemProxy.Get.OpenSaveDataFileSystemBySystemSaveDataId(ref fileSystem.Ref(), spaceId, in attribute);
            if (rc.IsFailure()) return rc;

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