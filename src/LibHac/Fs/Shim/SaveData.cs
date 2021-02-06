using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    public static class SaveData
    {
        public static Result MountSaveData(this FileSystemClient fs, U8Span mountName, Ncm.ApplicationId applicationId, UserId userId)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                System.TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, applicationId, userId, SaveDataType.Account, false, 0);
                System.TimeSpan endTime = fs.Time.GetCurrent();

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
                System.TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, applicationId, userId, SaveDataType.Account, true, 0);
                System.TimeSpan endTime = fs.Time.GetCurrent();

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
                System.TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.Temporary, default, default, SaveDataType.Temporary, false, 0);
                System.TimeSpan endTime = fs.Time.GetCurrent();

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
                System.TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, default, default, SaveDataType.Cache, false, 0);
                System.TimeSpan endTime = fs.Time.GetCurrent();

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
                System.TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, default, default, SaveDataType.Cache, false, (ushort)index);
                System.TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName.ToString()}\", index: {index}");
            }
            else
            {
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, default, default, SaveDataType.Cache, false, (ushort)index);
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
                System.TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, applicationId, default, SaveDataType.Cache, false, 0);
                System.TimeSpan endTime = fs.Time.GetCurrent();

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
                System.TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, applicationId, default, SaveDataType.Cache, false, (ushort)index);
                System.TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName.ToString()}\", applicationid: 0x{applicationId}, index: {index}");
            }
            else
            {
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, applicationId, default, SaveDataType.Cache, false, (ushort)index);
            }

            if (rc.IsSuccess() && fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return rc;
        }

        private static Result MountSaveDataImpl(this FileSystemClient fs, U8Span mountName, SaveDataSpaceId spaceId,
            ProgramId programId, UserId userId, SaveDataType type, bool openReadOnly, ushort index)
        {
            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            var attribute = new SaveDataAttribute(programId, type, userId, 0, index);

            ReferenceCountedDisposable<IFileSystemSf> saveFs = null;

            try
            {
                if (openReadOnly)
                {
                    rc = fsProxy.Target.OpenReadOnlySaveDataFileSystem(out saveFs, spaceId, in attribute);
                }
                else
                {
                    rc = fsProxy.Target.OpenSaveDataFileSystem(out saveFs, spaceId, in attribute);
                }

                if (rc.IsFailure()) return rc;

                var fileSystemAdapter = new FileSystemServiceObjectAdapter(saveFs);

                return fs.Register(mountName, fileSystemAdapter, fileSystemAdapter, null);
            }
            finally
            {
                saveFs?.Dispose();
            }
        }
    }
}
