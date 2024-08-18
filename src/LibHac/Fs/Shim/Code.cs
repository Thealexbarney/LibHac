using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Sf;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for mounting code file systems.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
[SkipLocalsInit]
public static class Code
{
    public static Result MountCode(this FileSystemClient fs, out CodeVerificationData verificationData,
        U8Span mountName, U8Span path, ContentAttributes attributes, ProgramId programId)
    {
        Result res;
        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Mount(fs, out verificationData, mountName, path, attributes, programId);
            Tick end = fs.Hos.Os.GetSystemTick();

            Span<byte> logBuffer = stackalloc byte[0x300];
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogPath).Append(path).Append(LogQuote)
                .Append(LogProgramId).AppendFormat(programId.Value, 'X');

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = Mount(fs, out verificationData, mountName, path, attributes, programId);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result Mount(FileSystemClient fs, out CodeVerificationData verificationData,
            U8Span mountName, U8Span path, ContentAttributes attributes, ProgramId programId)
        {
            UnsafeHelpers.SkipParamInit(out verificationData);

            Result res = fs.Impl.CheckMountName(mountName);
            if (res.IsFailure()) return res.Miss();

            res = PathUtility.ConvertToFspPath(out FspPath sfPath, path);
            if (res.IsFailure()) return res.Miss();

            using SharedRef<IFileSystemProxyForLoader> fileSystemProxy =
                fs.Impl.GetFileSystemProxyForLoaderServiceObject();

            // Todo: IPC code should automatically set process ID
            res = fileSystemProxy.Get.SetCurrentProcess(fs.Hos.Os.GetCurrentProcessId().Value);
            if (res.IsFailure()) return res.Miss();

            using var fileSystem = new SharedRef<IFileSystemSf>();

            res = fileSystemProxy.Get.OpenCodeFileSystem(ref fileSystem.Ref, OutBuffer.FromStruct(ref verificationData),
                in sfPath, attributes, programId);
            if (res.IsFailure()) return res.Miss();

            using var fileSystemAdapter = new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(in fileSystem));

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInCodeA.Log();

            res = fs.Register(mountName, ref fileSystemAdapter.Ref);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }
}