using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    public static class Application
    {
        public static Result MountApplicationPackage(this FileSystemClient fs, U8Span mountName, U8Span path)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                System.TimeSpan startTime = fs.Time.GetCurrent();
                rc = Run(fs, mountName, path);
                System.TimeSpan endTime = fs.Time.GetCurrent();

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

                FspPath.FromSpan(out FspPath sfPath, path);

                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                rc = fsProxy.Target.OpenFileSystemWithId(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
                    in sfPath, default, FileSystemProxyType.Package);
                if (rc.IsFailure()) return rc;

                using (fileSystem)
                {
                    var fileSystemAdapter = new FileSystemServiceObjectAdapter(fileSystem);

                    return fs.Register(mountName, fileSystemAdapter);
                }
            }
        }
    }
}
