using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
    /// <summary>
    /// Contains functions for mounting code file systems.
    /// </summary>
    /// <remarks>Based on FS 12.1.0 (nnSdk 12.3.1)</remarks>
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

                rc = PathUtility.ConvertToFspPath(out FspPath fsPath, path);
                if (rc.IsFailure()) return rc;

                using SharedRef<IFileSystemProxyForLoader> fileSystemProxy =
                    fs.Impl.GetFileSystemProxyForLoaderServiceObject();

                // SetCurrentProcess

                using var fileSystem = new SharedRef<IFileSystemSf>();

                rc = fileSystemProxy.Get.OpenCodeFileSystem(ref fileSystem.Ref(), out verificationData, in fsPath,
                    programId);
                if (rc.IsFailure()) return rc;

                using var fileSystemAdapter =
                    new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

                if (!fileSystemAdapter.HasValue)
                    return ResultFs.AllocationMemoryFailedInCodeA.Log();

                rc = fs.Register(mountName, ref fileSystemAdapter.Ref());
                if (rc.IsFailure()) return rc.Miss();

                return Result.Success;
            }
        }
    }
}
