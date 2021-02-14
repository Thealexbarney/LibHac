using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    public static class BcatSaveData
    {
        public static Result MountBcatSaveData(this FileSystemClient fs, U8Span mountName, Ncm.ApplicationId applicationId)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                System.TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountBcatSaveDataImpl(fs, mountName, applicationId);
                System.TimeSpan endTime = fs.Time.GetCurrent();

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

        private static Result MountBcatSaveDataImpl(FileSystemClient fs, U8Span mountName, Ncm.ApplicationId applicationId)
        {
            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            var attribute = new SaveDataAttribute(applicationId, SaveDataType.Bcat, UserId.InvalidId, 0);

            ReferenceCountedDisposable<IFileSystemSf> saveFs = null;

            try
            {
                rc = fsProxy.Target.OpenSaveDataFileSystem(out saveFs, SaveDataSpaceId.User, in attribute);
                if (rc.IsFailure()) return rc;

                var fileSystemAdapter = new FileSystemServiceObjectAdapter(saveFs);

                return fs.Register(mountName, fileSystemAdapter);
            }
            finally
            {
                saveFs?.Dispose();
            }
        }
    }
}
