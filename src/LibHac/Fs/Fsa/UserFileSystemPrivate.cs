using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;

namespace LibHac.Fs.Fsa;

/// <summary>
/// Contains functions meant for internal use for interacting with mounted file systems.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0</remarks>
public static class UserFileSystemPrivate
{
    public static Result CreateFile(this FileSystemClient fs, U8Span path, long size, CreateFileOptions option)
    {
        Result res;
        U8Span subPath;
        FileSystemAccessor fileSystem;
        Span<byte> logBuffer = stackalloc byte[0x300];

        if (fs.Impl.IsEnabledAccessLog())
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = fs.Impl.FindFileSystem(out fileSystem, out subPath, path);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogPath).Append(path).Append(LogQuote);
            logBuffer = sb.Buffer;

            fs.Impl.OutputAccessLogUnlessResultSuccess(res, start, end, null, new U8Span(logBuffer));
        }
        else
        {
            res = fs.Impl.FindFileSystem(out fileSystem, out subPath, path);
        }
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        if (fs.Impl.IsEnabledAccessLog() && fileSystem.IsEnabledAccessLog())
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            res = fileSystem.CreateFile(subPath, size, option);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogPath).Append(path).Append(LogQuote).Append(LogSize).AppendFormat(size);
            logBuffer = sb.Buffer;

            fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(logBuffer));
        }
        else
        {
            res = fileSystem.CreateFile(subPath, size, option);
        }
        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static Result GetTotalSpaceSize(this FileSystemClient fs, out long totalSpace, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out totalSpace);

        Result res = fs.Impl.FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.GetTotalSpaceSize(out totalSpace, subPath);
        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static Result SetConcatenationFileAttribute(this FileSystemClient fs, U8Span path)
    {
        Result res = fs.Impl.FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.QueryEntry(Span<byte>.Empty, ReadOnlySpan<byte>.Empty, QueryId.SetConcatenationFileAttribute,
            subPath);
        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static Result QueryUnpreparedFileInformation(this FileSystemClient fs, out UnpreparedFileInformation info,
        U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out info);

        Result res = fs.Impl.FindFileSystem(out FileSystemAccessor fileSystem, out _, path);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.QueryEntry(SpanHelpers.AsByteSpan(ref info), ReadOnlySpan<byte>.Empty,
            QueryId.QueryUnpreparedFileInformation, new U8Span("/"u8));
        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    private static void SetOrChangeMin(ref int minValue, ref bool hasMinValue, int value, bool hasValue)
    {
        if (!hasValue)
            return;

        if (!hasMinValue)
        {
            minValue = value;
            hasMinValue = true;
            return;
        }

        if (minValue > value)
        {
            minValue = value;
        }
    }

    public static Result GetPathLengthMax(this FileSystemClient fs, out long outLength, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out outLength);

        Result res = fs.Impl.FindFileSystem(out FileSystemAccessor fileSystem, out _, path);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.GetFileSystemAttribute(out FileSystemAttribute attribute);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        int retValue = 0;
        bool hasRetValue = false;
        SetOrChangeMin(ref retValue, ref hasRetValue, attribute.DirectoryPathLengthMax, attribute.DirectoryPathLengthMaxHasValue);
        SetOrChangeMin(ref retValue, ref hasRetValue, attribute.FilePathLengthMax, attribute.FileNameLengthMaxHasValue);
        SetOrChangeMin(ref retValue, ref hasRetValue, FspPath.MaxLength, true);

        Assert.SdkAssert(hasRetValue);

        outLength = retValue;
        return Result.Success;
    }

    public static Result GetEntryNameLengthMax(this FileSystemClient fs, out long outLength, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out outLength);

        Result res = fs.Impl.FindFileSystem(out FileSystemAccessor fileSystem, out _, path);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.GetFileSystemAttribute(out FileSystemAttribute attribute);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        int retValue = 0;
        bool hasRetValue = false;
        SetOrChangeMin(ref retValue, ref hasRetValue, attribute.DirectoryNameLengthMax, attribute.DirectoryNameLengthMaxHasValue);
        SetOrChangeMin(ref retValue, ref hasRetValue, attribute.FileNameLengthMax, attribute.FileNameLengthMaxHasValue);
        SetOrChangeMin(ref retValue, ref hasRetValue, FspPath.MaxLength, true);

        Assert.SdkAssert(hasRetValue);

        outLength = retValue;
        return Result.Success;
    }

    public static Result GetUtf16PathLengthMax(this FileSystemClient fs, out bool outHasValue, out long outLength, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out outHasValue, out outLength);

        Result res = fs.Impl.FindFileSystem(out FileSystemAccessor fileSystem, out _, path);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.GetFileSystemAttribute(out FileSystemAttribute attribute);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        int retValue = 0;
        bool hasRetValue = false;
        SetOrChangeMin(ref retValue, ref hasRetValue, attribute.Utf16CreateDirectoryPathLengthMax, attribute.Utf16CreateDirectoryPathLengthMaxHasValue);
        SetOrChangeMin(ref retValue, ref hasRetValue, attribute.Utf16DeleteDirectoryPathLengthMax, attribute.Utf16DeleteDirectoryPathLengthMaxHasValue);
        SetOrChangeMin(ref retValue, ref hasRetValue, attribute.Utf16RenameSourceDirectoryPathLengthMax, attribute.Utf16RenameSourceDirectoryPathLengthMaxHasValue);
        SetOrChangeMin(ref retValue, ref hasRetValue, attribute.Utf16RenameDestinationDirectoryPathLengthMax, attribute.Utf16RenameDestinationDirectoryPathLengthMaxHasValue);
        SetOrChangeMin(ref retValue, ref hasRetValue, attribute.Utf16OpenDirectoryPathLengthMax, attribute.Utf16OpenDirectoryPathLengthMaxHasValue);
        SetOrChangeMin(ref retValue, ref hasRetValue, attribute.Utf16FilePathLengthMax, attribute.Utf16FilePathLengthMaxHasValue);
        SetOrChangeMin(ref retValue, ref hasRetValue, attribute.Utf16DirectoryPathLengthMax, attribute.Utf16DirectoryPathLengthMaxHasValue);

        outHasValue = hasRetValue;
        outLength = retValue;
        return Result.Success;
    }

    public static Result GetUtf16EntryNameLengthMax(this FileSystemClient fs, out bool outHasValue, out long outLength, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out outHasValue, out outLength);

        Result res = fs.Impl.FindFileSystem(out FileSystemAccessor fileSystem, out _, path);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.GetFileSystemAttribute(out FileSystemAttribute attribute);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        int retValue = 0;
        bool hasRetValue = false;
        SetOrChangeMin(ref retValue, ref hasRetValue, attribute.Utf16FileNameLengthMax, attribute.Utf16FileNameLengthMaxHasValue);
        SetOrChangeMin(ref retValue, ref hasRetValue, attribute.Utf16DirectoryNameLengthMax, attribute.Utf16DirectoryNameLengthMaxHasValue);

        outHasValue = hasRetValue;
        outLength = retValue;
        return Result.Success;
    }

    public static Result GetFileSystemAttributeForDebug(this FileSystemClient fs, out FileSystemAttribute outAttribute, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out outAttribute);

        Result res = fs.Impl.FindFileSystem(out FileSystemAccessor fileSystem, out _, path);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.GetFileSystemAttribute(out outAttribute);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}