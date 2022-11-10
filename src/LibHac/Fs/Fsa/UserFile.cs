using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;

namespace LibHac.Fs.Fsa;

/// <summary>
/// Contains functions for interacting with opened files.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
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
        Result res = ReadFileImpl(fs, out long bytesRead, handle, offset, destination, in option);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (bytesRead == destination.Length)
            return Result.Success;

        res = ResultFs.OutOfRange.Log();
        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static Result ReadFile(this FileSystemClient fs, FileHandle handle, long offset, Span<byte> destination)
    {
        Result res = ReadFileImpl(fs, out long bytesRead, handle, offset, destination, ReadOption.None);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (bytesRead == destination.Length)
            return Result.Success;

        res = ResultFs.OutOfRange.Log();
        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static Result ReadFile(this FileSystemClient fs, out long bytesRead, FileHandle handle, long offset,
        Span<byte> destination, in ReadOption option)
    {
        Result res = ReadFileImpl(fs, out bytesRead, handle, offset, destination, in option);
        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static Result ReadFile(this FileSystemClient fs, out long bytesRead, FileHandle handle, long offset,
        Span<byte> destination)
    {
        Result res = ReadFileImpl(fs, out bytesRead, handle, offset, destination, ReadOption.None);
        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static Result WriteFile(this FileSystemClient fs, FileHandle handle, long offset,
        ReadOnlySpan<byte> source, in WriteOption option)
    {
        Result res;

        if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(handle))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Get(handle).Write(offset, source, in option);
            Tick end = fs.Hos.Os.GetSystemTick();

            Span<byte> buffer = stackalloc byte[0x60];
            var sb = new U8StringBuilder(buffer, true);

            sb.Append(LogOffset).AppendFormat(offset).Append(LogSize).AppendFormat(source.Length);

            if (option.HasFlushFlag())
                sb.Append(LogWriteOptionFlush);

            fs.Impl.OutputAccessLog(res, start, end, handle, new U8Span(sb.Buffer));
            sb.Dispose();
        }
        else
        {
            res = Get(handle).Write(offset, source, in option);
        }

        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static Result FlushFile(this FileSystemClient fs, FileHandle handle)
    {
        Result res;

        if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(handle))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Get(handle).Flush();
            Tick end = fs.Hos.Os.GetSystemTick();

            fs.Impl.OutputAccessLog(res, start, end, handle, U8Span.Empty);
        }
        else
        {
            res = Get(handle).Flush();
        }

        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static Result SetFileSize(this FileSystemClient fs, FileHandle handle, long size)
    {
        Result res;

        if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(handle))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Get(handle).SetSize(size);
            Tick end = fs.Hos.Os.GetSystemTick();

            Span<byte> buffer = stackalloc byte[0x20];
            var sb = new U8StringBuilder(buffer, true);

            sb.Append(LogSize).AppendFormat(size);
            fs.Impl.OutputAccessLog(res, start, end, handle, new U8Span(sb.Buffer));
        }
        else
        {
            res = Get(handle).SetSize(size);
        }

        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static Result GetFileSize(this FileSystemClient fs, out long size, FileHandle handle)
    {
        Result res;

        if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(handle))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = Get(handle).GetSize(out size);
            Tick end = fs.Hos.Os.GetSystemTick();

            Span<byte> buffer = stackalloc byte[0x20];
            var sb = new U8StringBuilder(buffer, true);

            sb.Append(LogSize).AppendFormat(AccessLogImpl.DereferenceOutValue(in size, res));
            fs.Impl.OutputAccessLog(res, start, end, handle, new U8Span(sb.Buffer));
        }
        else
        {
            res = Get(handle).GetSize(out size);
        }

        fs.Impl.AbortIfNeeded(res);
        return res;
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

        Result res = Get(handle).OperateRange(SpanHelpers.AsByteSpan(ref rangeInfo), OperationId.QueryRange, offset,
            size, ReadOnlySpan<byte>.Empty);

        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static Result InvalidateCache(this FileSystemClient fs, FileHandle handle)
    {
        Result res = Get(handle).OperateRange(Span<byte>.Empty, OperationId.InvalidateCache, 0, long.MaxValue,
            ReadOnlySpan<byte>.Empty);

        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static Result QueryUnpreparedRange(this FileSystemClient fs, out Range unpreparedRange, FileHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out unpreparedRange);
        Unsafe.SkipInit(out UnpreparedRangeInfo info);

        Result res = Get(handle).OperateRange(SpanHelpers.AsByteSpan(ref info), OperationId.QueryUnpreparedRange, 0, 0,
            ReadOnlySpan<byte>.Empty);

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        unpreparedRange = info.Range;
        return Result.Success;
    }

    public static Result QueryUnpreparedRangeDetail(this FileSystemClient fs,
        out UnpreparedRangeInfo unpreparedRangeInfo, FileHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out unpreparedRangeInfo);
        Unsafe.SkipInit(out UnpreparedRangeInfo info);

        Result res = Get(handle).OperateRange(SpanHelpers.AsByteSpan(ref info), OperationId.QueryUnpreparedRange, 0, 0,
            ReadOnlySpan<byte>.Empty);

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        unpreparedRangeInfo = info;
        return Result.Success;
    }

    public static Result QueryLazyLoadCompletionRate(this FileSystemClient fs, out int completionRate,
        FileHandle handle, int guideIndex)
    {
        UnsafeHelpers.SkipParamInit(out completionRate);

        Unsafe.SkipInit(out UnpreparedRangeInfo info);
        var args = new LazyLoadArguments { GuideIndex = guideIndex };

        Result res = Get(handle).OperateRange(SpanHelpers.AsByteSpan(ref info), OperationId.QueryLazyLoadCompletionRate,
            0, 0, SpanHelpers.AsReadOnlyByteSpan(in args));

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        completionRate = info.CompletionRate;
        return Result.Success;
    }

    public static Result ReadyLazyLoadFileForciblyForDebug(this FileSystemClient fs, FileHandle handle, long offset,
        long size)
    {
        Unsafe.SkipInit(out UnpreparedRangeInfo info);

        Result res = Get(handle).OperateRange(SpanHelpers.AsByteSpan(ref info), OperationId.ReadyLazyLoadFile,
            offset, size, ReadOnlySpan<byte>.Empty);

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}