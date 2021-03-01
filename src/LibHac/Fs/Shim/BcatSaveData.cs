using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
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

                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Bcat,
                    Fs.SaveData.InvalidUserId, 0);
                if (rc.IsFailure()) return rc;

                ReferenceCountedDisposable<IFileSystemSf> fileSystem = null;
                try
                {
                    rc = fsProxy.Target.OpenSaveDataFileSystem(out fileSystem, SaveDataSpaceId.User, in attribute);
                    if (rc.IsFailure()) return rc;

                    var fileSystemAdapter = new FileSystemServiceObjectAdapter(fileSystem);
                    return fs.Register(mountName, fileSystemAdapter);
                }
                finally
                {
                    fileSystem?.Dispose();
                }
            }
        }
    }
}
