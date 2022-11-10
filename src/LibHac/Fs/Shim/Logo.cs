using System;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Os;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

using static LibHac.Fs.Impl.AccessLogStrings;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for opening the logo partitions of NCA files.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class Logo
{
    public static Result MountLogo(this FileSystemClient fs, U8Span mountName, U8Span path, ProgramId programId)
    {
        Result res;
        Span<byte> logBuffer = stackalloc byte[0x300];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Mount(fs, mountName, path, programId);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogPath).Append(path).Append(LogQuote)
                .Append(LogProgramId).AppendFormat(programId.Value, 'X');

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = Mount(fs, mountName, path, programId);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result Mount(FileSystemClient fs, U8Span mountName, U8Span path, ProgramId programId)
        {
            Result res = fs.Impl.CheckMountName(mountName);
            if (res.IsFailure()) return res.Miss();

            res = PathUtility.ConvertToFspPath(out FspPath sfPath, path);
            if (res.IsFailure()) return res.Miss();

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var fileSystem = new SharedRef<IFileSystemSf>();

            res = fileSystemProxy.Get.OpenFileSystemWithId(ref fileSystem.Ref(), in sfPath, programId.Value,
                FileSystemProxyType.Logo);
            if (res.IsFailure()) return res.Miss();

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInLogoA.Log();

            res = fs.Impl.Fs.Register(mountName, ref fileSystemAdapter.Ref());
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }
}