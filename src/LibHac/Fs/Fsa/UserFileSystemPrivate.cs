using System;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;

namespace LibHac.Fs.Fsa
{
    public static class UserFileSystemPrivate
    {
        public static Result CreateFile(this FileSystemClient fs, U8Span path, long size, CreateFileOptions options)
        {
            Result rc;
            U8Span subPath;
            FileSystemAccessor fileSystem;
            Span<byte> logBuffer = stackalloc byte[0x300];

            if (fs.Impl.IsEnabledAccessLog())
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.FindFileSystem(out fileSystem, out subPath, path);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogPath).Append(path).Append((byte)'"');
                logBuffer = sb.Buffer;

                fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = fs.Impl.FindFileSystem(out fileSystem, out subPath, path);
            }
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog() && fileSystem.IsEnabledAccessLog())
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fileSystem.CreateFile(subPath, size, options);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogPath).Append(path).Append((byte)'"').Append(LogSize).AppendFormat(size);
                logBuffer = sb.Buffer;

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(logBuffer));
            }
            else
            {
                rc = fileSystem.CreateFile(subPath, size, options);
            }
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result GetTotalSpaceSize(this FileSystemClient fs, out long totalSpace, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out totalSpace);

            Result rc = fs.Impl.FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            rc = fileSystem.GetFreeSpaceSize(out totalSpace, subPath);
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result SetConcatenationFileAttribute(this FileSystemClient fs, U8Span path)
        {
            Result rc = fs.Impl.FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            rc = fileSystem.QueryEntry(Span<byte>.Empty, ReadOnlySpan<byte>.Empty, QueryId.MakeConcatFile, subPath);
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }
    }
}
