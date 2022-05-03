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
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x300];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = Mount(fs, mountName, path, programId);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogPath).Append(path).Append(LogQuote)
                .Append(LogProgramId).AppendFormat(programId.Value, 'X');

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = Mount(fs, mountName, path, programId);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result Mount(FileSystemClient fs, U8Span mountName, U8Span path, ProgramId programId)
        {
            Result rc = fs.Impl.CheckMountName(mountName);
            if (rc.IsFailure()) return rc.Miss();

            rc = PathUtility.ConvertToFspPath(out FspPath sfPath, path);
            if (rc.IsFailure()) return rc;

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var fileSystem = new SharedRef<IFileSystemSf>();

            rc = fileSystemProxy.Get.OpenFileSystemWithId(ref fileSystem.Ref(), in sfPath, programId.Value,
                FileSystemProxyType.Logo);
            if (rc.IsFailure()) return rc.Miss();

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInLogoA.Log();

            rc = fs.Impl.Fs.Register(mountName, ref fileSystemAdapter.Ref());
            if (rc.IsFailure()) return rc.Miss();

            return Result.Success;
        }
    }
}