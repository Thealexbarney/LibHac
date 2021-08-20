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

namespace LibHac.Fs.Shim
{
    /// <summary>
    /// Contains functions for mounting BCAT save data.
    /// </summary>
    /// <remarks>Based on FS 12.1.0 (nnSdk 12.3.1)</remarks>
    [SkipLocalsInit]
    public static class BcatSaveData
    {
        public static Result MountBcatSaveData(this FileSystemClient fs, U8Span mountName,
            Ncm.ApplicationId applicationId)
        {
            Result rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = Mount(fs, mountName, applicationId);
                Tick end = fs.Hos.Os.GetSystemTick();

                Span<byte> logBuffer = stackalloc byte[0x50];
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogName).Append(mountName).Append(LogQuote)
                    .Append(LogApplicationId).AppendFormat(applicationId.Value, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = Mount(fs, mountName, applicationId);
            }

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
                fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

            return Result.Success;

            static Result Mount(FileSystemClient fs, U8Span mountName, Ncm.ApplicationId applicationId)
            {
                Result rc = fs.Impl.CheckMountName(mountName);
                if (rc.IsFailure()) return rc;

                using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

                rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Bcat,
                    Fs.SaveData.InvalidUserId, 0);
                if (rc.IsFailure()) return rc;

                using var fileSystem = new SharedRef<IFileSystemSf>();

                rc = fileSystemProxy.Get.OpenSaveDataFileSystem(ref fileSystem.Ref(), SaveDataSpaceId.User, in attribute);
                if (rc.IsFailure()) return rc;

                using var fileSystemAdapter =
                    new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

                if (!fileSystemAdapter.HasValue)
                    return ResultFs.AllocationMemoryFailedInBcatSaveDataA.Log();

                rc = fs.Register(mountName, ref fileSystemAdapter.Ref());
                if (rc.IsFailure()) return rc.Miss();

                return Result.Success;
            }
        }
    }
}
