using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    public static class Code
    {
        public static Result MountCode(this FileSystemClient fs, out CodeVerificationData verificationData,
            U8Span mountName, U8Span path, ProgramId programId)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                System.TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountCodeImpl(fs, out verificationData, mountName, path, programId);
                System.TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime,
                    $", name: \"{mountName.ToString()}\", name: \"{path.ToString()}\", programid: 0x{programId}");
            }
            else
            {
                rc = MountCodeImpl(fs, out verificationData, mountName, path, programId);
            }

            if (rc.IsSuccess() && fs.IsEnabledAccessLog(AccessLogTarget.System))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return rc;
        }

        private static Result MountCodeImpl(this FileSystemClient fs, out CodeVerificationData verificationData,
            U8Span mountName, U8Span path, ProgramId programId)
        {
            Unsafe.SkipInit(out verificationData);

            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            rc = FspPath.FromSpan(out FspPath fsPath, path);
            if (rc.IsFailure()) return rc;

            IFileSystemProxyForLoader fsProxy = fs.GetFileSystemProxyForLoaderServiceObject();

            rc = fsProxy.OpenCodeFileSystem(out ReferenceCountedDisposable<IFileSystemSf> codeFs, out verificationData,
                in fsPath, programId);
            if (rc.IsFailure()) return rc;

            using (codeFs)
            {
                var fileSystemAdapter = new FileSystemServiceObjectAdapter(codeFs);

                return fs.Register(mountName, fileSystemAdapter);
            }
        }
    }
}
