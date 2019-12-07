using System;
using LibHac.Common;
using LibHac.FsService;
using LibHac.Ncm;

namespace LibHac.Fs.Shim
{
    public static class SaveData
    {
        public static Result MountSaveData(this FileSystemClient fs, U8Span mountName, TitleId titleId, UserId userId)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, titleId, userId, SaveDataType.Account, false, 0);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName.ToString()}\", applicationid: 0x{titleId}, userid: 0x{userId}");
            }
            else
            {
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, titleId, userId, SaveDataType.Account, false, 0);
            }

            if (rc.IsSuccess() && fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return rc;
        }

        public static Result MountSaveDataReadOnly(this FileSystemClient fs, U8Span mountName, TitleId titleId, UserId userId)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, titleId, userId, SaveDataType.Account, true, 0);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName.ToString()}\", applicationid: 0x{titleId}, userid: 0x{userId}");
            }
            else
            {
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, titleId, userId, SaveDataType.Account, false, 0);
            }

            if (rc.IsSuccess() && fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return rc;
        }

        private static Result MountSaveDataImpl(this FileSystemClient fs, U8Span mountName, SaveDataSpaceId spaceId,
            TitleId titleId, UserId userId, SaveDataType type, bool openReadOnly, short index)
        {
            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            SaveDataAttribute attribute = default;
            attribute.TitleId = titleId;
            attribute.UserId = userId;
            attribute.Type = type;
            attribute.Index = index;

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
