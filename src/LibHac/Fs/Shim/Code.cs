using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    [SkipLocalsInit]
    public static class Code
    {
        public static Result MountCode(this FileSystemClient fs, out CodeVerificationData verificationData,
            U8Span mountName, U8Span path, ProgramId programId)
        {
            Result rc;
            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = Mount(fs, out verificationData, mountName, path, programId);
                Tick end = fs.Hos.Os.GetSystemTick();

                Span<byte> logBuffer = stackalloc byte[0x300];
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogName).Append(mountName).Append(LogQuote)
                    .Append(LogPath).Append(path).Append(LogQuote)
                    .Append(LogProgramId).AppendFormat(programId.Value, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = Mount(fs, out verificationData, mountName, path, programId);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
                fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

            return Result.Success;

            static Result Mount(FileSystemClient fs, out CodeVerificationData verificationData,
                U8Span mountName, U8Span path, ProgramId programId)
            {
                UnsafeHelpers.SkipParamInit(out verificationData);

                Result rc = fs.Impl.CheckMountName(mountName);
                if (rc.IsFailure()) return rc;

                if (path.IsNull())
                    return ResultFs.NullptrArgument.Log();

                rc = FspPath.FromSpan(out FspPath fsPath, path);
                if (rc.IsFailure()) return rc;

                using ReferenceCountedDisposable<IFileSystemProxyForLoader> fsProxy =
                    fs.Impl.GetFileSystemProxyForLoaderServiceObject();

                ReferenceCountedDisposable<IFileSystemSf> fileSystem = null;
                try
                {
                    rc = fsProxy.Target.OpenCodeFileSystem(out fileSystem, out verificationData, in fsPath, programId);
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
