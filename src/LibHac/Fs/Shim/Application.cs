using System;
using LibHac.Common;
using LibHac.FsService;
using LibHac.FsSystem;

namespace LibHac.Fs.Shim
{
    public static class Application
    {
        public static Result MountApplicationPackage(this FileSystemClient fs, U8Span mountName, U8Span path)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = Run(fs, mountName, path);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, "");
            }
            else
            {
                rc = Run(fs, mountName, path);
            }

            if (rc.IsFailure()) return rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return Result.Success;

            static Result Run(FileSystemClient fs, U8Span mountName, U8Span path)
            {
                // ReSharper disable once VariableHidesOuterVariable
                Result rc = MountHelpers.CheckMountName(mountName);
                if (rc.IsFailure()) return rc;

                FsPath.FromSpan(out FsPath fsPath, path);

                IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                rc = fsProxy.OpenFileSystemWithId(out IFileSystem fileSystem, ref fsPath, default, FileSystemProxyType.Package);
                if (rc.IsFailure()) return rc;

                return fs.Register(mountName, fileSystem);
            }
        }
    }
}
