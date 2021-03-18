using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;

namespace LibHac.Fs.Fsa
{
    [SkipLocalsInit]
    public static class UserFile
    {
        private static FileAccessor Get(FileHandle handle)
        {
            return handle.File;
        }

        private static Result ReadFileImpl(FileSystemClient fs, out long bytesRead, FileHandle handle, long offset,
            Span<byte> destination, in ReadOption option)
        {
            return Get(handle).Read(out bytesRead, offset, destination, in option);
        }

        public static Result ReadFile(this FileSystemClient fs, FileHandle handle, long offset, Span<byte> destination,
            in ReadOption option)
        {
            Result rc = ReadFileImpl(fs, out long bytesRead, handle, offset, destination, in option);
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (bytesRead == destination.Length)
                return Result.Success;

            rc = ResultFs.OutOfRange.Log();
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result ReadFile(this FileSystemClient fs, FileHandle handle, long offset, Span<byte> destination)
        {
            Result rc = ReadFileImpl(fs, out long bytesRead, handle, offset, destination, ReadOption.None);
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (bytesRead == destination.Length)
                return Result.Success;

            rc = ResultFs.OutOfRange.Log();
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result ReadFile(this FileSystemClient fs, out long bytesRead, FileHandle handle, long offset,
            Span<byte> destination, in ReadOption option)
        {
            Result rc = ReadFileImpl(fs, out bytesRead, handle, offset, destination, in option);
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result ReadFile(this FileSystemClient fs, out long bytesRead, FileHandle handle, long offset,
            Span<byte> destination)
        {
            Result rc = ReadFileImpl(fs, out bytesRead, handle, offset, destination, ReadOption.None);
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result WriteFile(this FileSystemClient fs, FileHandle handle, long offset,
            ReadOnlySpan<byte> source, in WriteOption option)
        {
            Result rc;

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(handle))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = Get(handle).Write(offset, source, in option);
                Tick end = fs.Hos.Os.GetSystemTick();

                Span<byte> buffer = stackalloc byte[0x60];
                var sb = new U8StringBuilder(buffer, true);

                sb.Append(LogOffset).AppendFormat(offset).Append(LogSize).AppendFormat(source.Length);

                if (option.HasFlushFlag())
                    sb.Append(LogWriteOptionFlush);

                fs.Impl.OutputAccessLog(rc, start, end, handle, new U8Span(sb.Buffer));
                sb.Dispose();
            }
            else
            {
                rc = Get(handle).Write(offset, source, in option);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result FlushFile(this FileSystemClient fs, FileHandle handle)
        {
            Result rc;

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(handle))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = Get(handle).Flush();
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLog(rc, start, end, handle, U8Span.Empty);
            }
            else
            {
                rc = Get(handle).Flush();
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result SetFileSize(this FileSystemClient fs, FileHandle handle, long size)
        {
            Result rc;

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(handle))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = Get(handle).SetSize(size);
                Tick end = fs.Hos.Os.GetSystemTick();

                Span<byte> buffer = stackalloc byte[0x20];
                var sb = new U8StringBuilder(buffer, true);

                sb.Append(LogSize).AppendFormat(size);
                fs.Impl.OutputAccessLog(rc, start, end, handle, new U8Span(sb.Buffer));
            }
            else
            {
                rc = Get(handle).SetSize(size);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result GetFileSize(this FileSystemClient fs, out long size, FileHandle handle)
        {
            Result rc;

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(handle))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = Get(handle).GetSize(out size);
                Tick end = fs.Hos.Os.GetSystemTick();

                Span<byte> buffer = stackalloc byte[0x20];
                var sb = new U8StringBuilder(buffer, true);

                sb.Append(LogSize).AppendFormat(AccessLogImpl.DereferenceOutValue(in size, rc));
                fs.Impl.OutputAccessLog(rc, start, end, handle, new U8Span(sb.Buffer));
            }
            else
            {
                rc = Get(handle).GetSize(out size);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static OpenMode GetFileOpenMode(this FileSystemClient fs, FileHandle handle)
        {
            OpenMode mode = Get(handle).GetOpenMode();

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(handle))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                Tick end = fs.Hos.Os.GetSystemTick();

                Span<byte> buffer = stackalloc byte[0x20];
                var sb = new U8StringBuilder(buffer, true);

                sb.Append(LogOpenMode).AppendFormat((int)mode, 'X');
                fs.Impl.OutputAccessLog(Result.Success, start, end, handle, new U8Span(sb.Buffer));
            }

            return mode;
        }

        public static void CloseFile(this FileSystemClient fs, FileHandle handle)
        {
            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(handle))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                Get(handle).Dispose();
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLog(Result.Success, start, end, handle, U8Span.Empty);
            }
            else
            {
                Get(handle).Dispose();
            }
        }

        public static Result QueryRange(this FileSystemClient fs, out QueryRangeInfo rangeInfo, FileHandle handle,
            long offset, long size)
        {
            UnsafeHelpers.SkipParamInit(out rangeInfo);

            Result rc = Get(handle).OperateRange(SpanHelpers.AsByteSpan(ref rangeInfo), OperationId.QueryRange, offset,
                size, ReadOnlySpan<byte>.Empty);

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result InvalidateCache(this FileSystemClient fs, FileHandle handle, long offset, long size)
        {
            Result rc = Get(handle).OperateRange(Span<byte>.Empty, OperationId.InvalidateCache, offset, size,
                ReadOnlySpan<byte>.Empty);

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }
    }
}
