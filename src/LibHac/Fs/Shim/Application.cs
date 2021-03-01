using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

namespace LibHac.Fs.Shim
{
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

                rc = FspPath.FromSpan(out FspPath sfPath, path);
                if (rc.IsFailure()) return rc;

                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                ReferenceCountedDisposable<IFileSystemSf> fileSystem = null;
                try
                {
                    rc = fsProxy.Target.OpenFileSystemWithId(out fileSystem, in sfPath, Ncm.ProgramId.InvalidId.Value,
                        FileSystemProxyType.Package);
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
