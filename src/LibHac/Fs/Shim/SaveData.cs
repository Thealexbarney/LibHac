﻿using System;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.FsService;
using LibHac.Ncm;

namespace LibHac.Fs.Shim
{
    public static class SaveData
    {
        public static Result MountSaveData(this FileSystemClient fs, U8Span mountName, Ncm.ApplicationId applicationId, UserId userId)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, applicationId, userId, SaveDataType.Account, false, 0);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName.ToString()}\", applicationid: 0x{applicationId}, userid: 0x{userId}");
            }
            else
            {
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, applicationId, userId, SaveDataType.Account, false, 0);
            }

            if (rc.IsSuccess() && fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return rc;
        }

        public static Result MountSaveDataReadOnly(this FileSystemClient fs, U8Span mountName, Ncm.ApplicationId applicationId, UserId userId)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, applicationId, userId, SaveDataType.Account, true, 0);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName.ToString()}\", applicationid: 0x{applicationId}, userid: 0x{userId}");
            }
            else
            {
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, applicationId, userId, SaveDataType.Account, false, 0);
            }

            if (rc.IsSuccess() && fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return rc;
        }

        public static Result MountTemporaryStorage(this FileSystemClient fs, U8Span mountName)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.Temporary, default, default, SaveDataType.Temporary, false, 0);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName.ToString()}\"");
            }
            else
            {
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.Temporary, default, default, SaveDataType.Temporary, false, 0);
            }

            if (rc.IsSuccess() && fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return rc;
        }

        public static Result MountCacheStorage(this FileSystemClient fs, U8Span mountName)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, default, default, SaveDataType.Cache, false, 0);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName.ToString()}\"");
            }
            else
            {
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, default, default, SaveDataType.Cache, false, 0);
            }

            if (rc.IsSuccess() && fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return rc;
        }

        public static Result MountCacheStorage(this FileSystemClient fs, U8Span mountName, int index)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, default, default, SaveDataType.Cache, false, (short)index);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName.ToString()}\", index: {index}");
            }
            else
            {
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, default, default, SaveDataType.Cache, false, (short)index);
            }

            if (rc.IsSuccess() && fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return rc;
        }

        public static Result MountCacheStorage(this FileSystemClient fs, U8Span mountName, Ncm.ApplicationId applicationId)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, applicationId, default, SaveDataType.Cache, false, 0);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName.ToString()}\", applicationid: 0x{applicationId}");
            }
            else
            {
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, applicationId, default, SaveDataType.Cache, false, 0);
            }

            if (rc.IsSuccess() && fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return rc;
        }

        public static Result MountCacheStorage(this FileSystemClient fs, U8Span mountName, Ncm.ApplicationId applicationId, int index)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, applicationId, default, SaveDataType.Cache, false, (short)index);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName.ToString()}\", applicationid: 0x{applicationId}, index: {index}");
            }
            else
            {
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, applicationId, default, SaveDataType.Cache, false, (short)index);
            }

            if (rc.IsSuccess() && fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return rc;
        }

        private static Result MountSaveDataImpl(this FileSystemClient fs, U8Span mountName, SaveDataSpaceId spaceId,
            ProgramId programId, UserId userId, SaveDataType type, bool openReadOnly, short index)
        {
            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            var attribute = new SaveDataAttribute(programId, type, userId, 0, index);

            IFileSystem saveFs;

            if (openReadOnly)
            {
                rc = fsProxy.OpenReadOnlySaveDataFileSystem(out saveFs, spaceId, ref attribute);
            }
            else
            {
                rc = fsProxy.OpenSaveDataFileSystem(out saveFs, spaceId, ref attribute);
            }

            if (rc.IsFailure()) return rc;

            return fs.Register(mountName, saveFs);
        }
    }
}
