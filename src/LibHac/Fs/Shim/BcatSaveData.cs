using System;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.FsService;
using LibHac.Ncm;

namespace LibHac.Fs.Shim
{
    public static class BcatSaveData
    {
        public static Result MountBcatSaveData(this FileSystemClient fs, U8Span mountName, TitleId applicationId)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountBcatSaveDataImpl(fs, mountName, applicationId);
                TimeSpan endTime = fs.Time.GetCurrent();

                string logMessage = $", name: \"{mountName.ToString()}\", applicationid: 0x{applicationId}\"";

                fs.OutputAccessLog(rc, startTime, endTime, logMessage);
            }
            else
            {
                rc = MountBcatSaveDataImpl(fs, mountName, applicationId);
            }

            if (rc.IsFailure()) return rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return Result.Success;
        }

        private static Result MountBcatSaveDataImpl(FileSystemClient fs, U8Span mountName, TitleId applicationId)
        {
            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            SaveDataAttribute attribute = default;
            attribute.TitleId = applicationId;
            attribute.Type = SaveDataType.Bcat;

            rc = fsProxy.OpenSaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId.User, ref attribute);
            if (rc.IsFailure()) return rc;

            return fs.Register(mountName, fileSystem);
        }
    }
}
