using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for mounting application packages.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
[SkipLocalsInit]
public static class Application
{
    public static Result MountApplicationPackage(this FileSystemClient fs, U8Span mountName, U8Span path)
    {
        Result res;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Mount(fs, mountName, path);
            Tick end = fs.Hos.Os.GetSystemTick();

            Span<byte> logBuffer = stackalloc byte[0x300];
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogPath).Append(path).Append(LogQuote);

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            res = Mount(fs, mountName, path);
        }

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result Mount(FileSystemClient fs, U8Span mountName, U8Span path)
        {
            Result res = fs.Impl.CheckMountName(mountName);
            if (res.IsFailure()) return res.Miss();

            res = PathUtility.ConvertToFspPath(out FspPath sfPath, path);
            if (res.IsFailure()) return res.Miss();

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var fileSystem = new SharedRef<IFileSystemSf>();

            res = fileSystemProxy.Get.OpenFileSystemWithId(ref fileSystem.Ref(), in sfPath,
                Ncm.ProgramId.InvalidId.Value, FileSystemProxyType.Package);
            if (res.IsFailure()) return res.Miss();

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInApplicationA.Log();

            res = fs.Register(mountName, ref fileSystemAdapter.Ref());
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }
}