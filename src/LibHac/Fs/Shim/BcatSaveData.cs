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
/// Contains functions for mounting BCAT save data.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
[SkipLocalsInit]
public static class BcatSaveData
{
    public static Result MountBcatSaveData(this FileSystemClient fs, U8Span mountName,
        Ncm.ApplicationId applicationId)
    {
        Result res;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Mount(fs, mountName, applicationId);
            Tick end = fs.Hos.Os.GetSystemTick();

            Span<byte> logBuffer = stackalloc byte[0x50];
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogApplicationId).AppendFormat(applicationId.Value, 'X');

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = Mount(fs, mountName, applicationId);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result Mount(FileSystemClient fs, U8Span mountName, Ncm.ApplicationId applicationId)
        {
            Result res = fs.Impl.CheckMountName(mountName);
            if (res.IsFailure()) return res.Miss();

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            res = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Bcat,
                InvalidUserId, InvalidSystemSaveDataId);
            if (res.IsFailure()) return res.Miss();

            using var fileSystem = new SharedRef<IFileSystemSf>();

            res = fileSystemProxy.Get.OpenSaveDataFileSystem(ref fileSystem.Ref, SaveDataSpaceId.User, in attribute);
            if (res.IsFailure()) return res.Miss();

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref));

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInBcatSaveDataA.Log();

            res = fs.Register(mountName, ref fileSystemAdapter.Ref);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }
}