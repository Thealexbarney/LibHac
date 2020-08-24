﻿using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;

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
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountCodeImpl(fs, out verificationData, mountName, path, programId);
                TimeSpan endTime = fs.Time.GetCurrent();

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

            rc = fsProxy.OpenCodeFileSystem(out IFileSystem codeFs, out verificationData, in fsPath, programId);
            if (rc.IsFailure()) return rc;

            return fs.Register(mountName, codeFs);
        }
    }
}
