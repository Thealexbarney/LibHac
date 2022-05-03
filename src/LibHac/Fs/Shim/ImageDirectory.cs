using System;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

using static LibHac.Fs.Impl.AccessLogStrings;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for mounting the directories where images and videos are saved.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class ImageDirectory
{
    public static Result MountImageDirectory(this FileSystemClient fs, U8Span mountName, ImageDirectoryId directoryId)
    {
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x50];

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = Mount(fs, mountName, directoryId);
            Tick end = fs.Hos.Os.GetSystemTick();

            var idString = new IdString();
            var sb = new U8StringBuilder(logBuffer, true);

            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogImageDirectoryId).Append(idString.ToString(directoryId));

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = Mount(fs, mountName, directoryId);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;

        static Result Mount(FileSystemClient fs, U8Span mountName, ImageDirectoryId directoryId)
        {
            Result rc = fs.Impl.CheckMountName(mountName);
            if (rc.IsFailure()) return rc.Miss();

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            using var fileSystem = new SharedRef<IFileSystemSf>();

            rc = fileSystemProxy.Get.OpenImageDirectoryFileSystem(ref fileSystem.Ref(), directoryId);
            if (rc.IsFailure()) return rc.Miss();

            using var fileSystemAdapter =
                new UniqueRef<IFileSystem>(new FileSystemServiceObjectAdapter(ref fileSystem.Ref()));

            if (!fileSystemAdapter.HasValue)
                return ResultFs.AllocationMemoryFailedInImageDirectoryA.Log();

            rc = fs.Impl.Fs.Register(mountName, ref fileSystemAdapter.Ref());
            if (rc.IsFailure()) return rc.Miss();

            return Result.Success;
        }
    }
}