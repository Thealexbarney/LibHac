using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
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
    public static class Content
    {
        private static FileSystemProxyType ConvertToFileSystemProxyType(ContentType type)
        {
            switch (type)
            {
                case ContentType.Meta: return FileSystemProxyType.Meta;
                case ContentType.Control: return FileSystemProxyType.Control;
                case ContentType.Manual: return FileSystemProxyType.Manual;
                case ContentType.Data: return FileSystemProxyType.Data;
                default:
                    Abort.UnexpectedDefault();
                    return default;
            }
        }

        private static Result MountContentImpl(FileSystemClient fs, U8Span mountName, U8Span path, ulong id,
            ContentType contentType)
        {
            Result rc = fs.Impl.CheckMountNameAcceptingReservedMountName(mountName);
            if (rc.IsFailure()) return rc;

            FileSystemProxyType fsType = ConvertToFileSystemProxyType(contentType);

            if (path.IsNull())
                return ResultFs.NullptrArgument.Log();

            rc = FspPath.FromSpan(out FspPath fsPath, path);
            if (rc.IsFailure()) return rc;

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            ReferenceCountedDisposable<IFileSystemSf> fileSystem = null;
            try
            {
                rc = fsProxy.Target.OpenFileSystemWithId(out fileSystem, in fsPath, id, fsType);
                if (rc.IsFailure()) return rc;

                var fileSystemAdapter = new FileSystemServiceObjectAdapter(fileSystem);
                return fs.Register(mountName, fileSystemAdapter);
            }
            finally
            {
                fileSystem?.Dispose();
            }
        }

        public static Result MountContent(this FileSystemClient fs, U8Span mountName, U8Span path,
            ContentType contentType)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x300];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = PreMount(contentType);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogName).Append(mountName).Append(LogQuote)
                    .Append(LogPath).Append(path).Append(LogQuote)
                    .Append(LogContentType).Append(idString.ToString(contentType));

                fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = PreMount(contentType);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            const ulong programId = 0;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = MountContentImpl(fs, mountName, path, programId, contentType);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogName).Append(mountName).Append(LogQuote)
                    .Append(LogPath).Append(path).Append(LogQuote)
                    .Append(LogProgramId).AppendFormat(programId, 'X')
                    .Append(LogContentType).Append(idString.ToString(contentType));

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = MountContentImpl(fs, mountName, path, 0, contentType);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
                fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

            return Result.Success;

            static Result PreMount(ContentType contentType)
            {
                if (contentType == ContentType.Meta)
                    return ResultFs.InvalidArgument.Log();

                return Result.Success;
            }
        }

        public static Result MountContent(this FileSystemClient fs, U8Span mountName, U8Span path, ProgramId programId,
            ContentType contentType)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x300];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = MountContentImpl(fs, mountName, path, programId.Value, contentType);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogName).Append(mountName).Append(LogQuote)
                    .Append(LogPath).Append(path).Append(LogQuote)
                    .Append(LogProgramId).AppendFormat(programId.Value, 'X')
                    .Append(LogContentType).Append(idString.ToString(contentType));

                logBuffer = sb.Buffer;

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = MountContentImpl(fs, mountName, path, programId.Value, contentType);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
                fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

            return Result.Success;
        }

        public static Result MountContent(this FileSystemClient fs, U8Span mountName, U8Span path, DataId dataId,
            ContentType contentType)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x300];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = MountContentImpl(fs, mountName, path, dataId.Value, contentType);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogName).Append(mountName).Append(LogQuote)
                    .Append(LogPath).Append(path).Append(LogQuote)
                    .Append(LogDataId).AppendFormat(dataId.Value, 'X')
                    .Append(LogContentType).Append(idString.ToString(contentType));

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = MountContentImpl(fs, mountName, path, dataId.Value, contentType);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
                fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

            return Result.Success;
        }

        public static Result MountContent(this FileSystemClient fs, U8Span mountName, ProgramId programId,
            ContentType contentType)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x300];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = Mount(fs, mountName, programId, contentType);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogName).Append(mountName).Append(LogQuote)
                    .Append(LogProgramId).AppendFormat(programId.Value, 'X')
                    .Append(LogContentType).Append(idString.ToString(contentType));

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = Mount(fs, mountName, programId, contentType);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System))
                fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

            return Result.Success;

            static Result Mount(FileSystemClient fs, U8Span mountName, ProgramId programId, ContentType contentType)
            {
                Result rc = fs.Impl.CheckMountNameAcceptingReservedMountName(mountName);
                if (rc.IsFailure()) return rc;

                FileSystemProxyType fsType = ConvertToFileSystemProxyType(contentType);

                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                ReferenceCountedDisposable<IFileSystemSf> fileSystem = null;
                try
                {
                    rc = fsProxy.Target.OpenFileSystemWithPatch(out fileSystem, programId, fsType);
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
