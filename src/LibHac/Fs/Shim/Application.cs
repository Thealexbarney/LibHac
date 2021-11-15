﻿using System;
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
/// <remarks>Based on FS 12.1.0 (nnSdk 12.3.1)</remarks>
[SkipLocalsInit]
public static class Application
{
    public static Result MountApplicationPackage(this FileSystemClient fs, U8Span mountName, U8Span path)
    {
        Result rc;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = Mount(fs, mountName, path);
            Tick end = fs.Hos.Os.GetSystemTick();

            Span<byte> logBuffer = stackalloc byte[0x300];
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogPath).Append(path).Append(LogQuote);

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = Mount(fs, mountName, path);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc;

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result Mount(FileSystemClient fs, U8Span mountName, U8Span path)
        {
            Result rc = fs.Impl.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            rc = PathUtility.ConvertToFspPath(out FspPath sfPath, path);
            if (rc.IsFailure()) return rc;

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var fileSystem = new SharedRef<IFileSystemSf>();

            rc = fileSystemProxy.Get.OpenFileSystemWithId(ref fileSystem.Ref(), in sfPath,
                Ncm.ProgramId.InvalidId.Value, FileSystemProxyType.Package);
            if (rc.IsFailure()) return rc;

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInApplicationA.Log();

            rc = fs.Register(mountName, ref fileSystemAdapter.Ref());
            if (rc.IsFailure()) return rc.Miss();

            return Result.Success;
        }
    }
}
