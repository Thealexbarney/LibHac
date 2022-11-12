using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.Tests.Fs;

public static class FsaExtensions
{
    private static Result SetUpPath(ref Path path, string value)
    {
        if (value is null)
            return ResultFs.NullptrArgument.Log();

        Result res = path.Initialize(StringUtils.StringToUtf8(value));
        if (res.IsFailure()) return res.Miss();

        res = path.Normalize(new PathFlags());
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result CreateFile(this IFileSystem fs, string path, long size, CreateFileOptions option)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.CreateFile(in pathNormalized, size, option);
    }

    public static Result CreateFile(this IFileSystem fs, string path, long size)
    {
        return CreateFile(fs, path, size, CreateFileOptions.None);
    }

    public static Result DeleteFile(this IFileSystem fs, string path)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.DeleteFile(in pathNormalized);
    }

    public static Result CreateDirectory(this IFileSystem fs, string path)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.CreateDirectory(in pathNormalized);
    }

    public static Result DeleteDirectory(this IFileSystem fs, string path)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.DeleteDirectory(in pathNormalized);
    }

    public static Result DeleteDirectoryRecursively(this IFileSystem fs, string path)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.DeleteDirectoryRecursively(in pathNormalized);
    }

    public static Result CleanDirectoryRecursively(this IFileSystem fs, string path)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.CleanDirectoryRecursively(in pathNormalized);
    }

    public static Result RenameFile(this IFileSystem fs, string currentPath, string newPath)
    {
        using var currentPathNormalized = new Path();
        Result res = SetUpPath(ref currentPathNormalized.Ref(), currentPath);
        if (res.IsFailure()) return res.Miss();

        using var newPathNormalized = new Path();
        res = SetUpPath(ref newPathNormalized.Ref(), newPath);
        if (res.IsFailure()) return res.Miss();

        return fs.RenameFile(in currentPathNormalized, in newPathNormalized);
    }

    public static Result RenameDirectory(this IFileSystem fs, string currentPath, string newPath)
    {
        using var currentPathNormalized = new Path();
        Result res = SetUpPath(ref currentPathNormalized.Ref(), currentPath);
        if (res.IsFailure()) return res.Miss();

        using var newPathNormalized = new Path();
        res = SetUpPath(ref newPathNormalized.Ref(), newPath);
        if (res.IsFailure()) return res.Miss();

        return fs.RenameDirectory(in currentPathNormalized, in newPathNormalized);
    }

    public static Result GetEntryType(this IFileSystem fs, out DirectoryEntryType entryType, string path)
    {
        UnsafeHelpers.SkipParamInit(out entryType);

        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.GetEntryType(out entryType, in pathNormalized);
    }

    public static Result GetFreeSpaceSize(this IFileSystem fs, out long freeSpace, string path)
    {
        UnsafeHelpers.SkipParamInit(out freeSpace);

        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.GetFreeSpaceSize(out freeSpace, in pathNormalized);
    }

    public static Result GetTotalSpaceSize(this IFileSystem fs, out long totalSpace, string path)
    {
        UnsafeHelpers.SkipParamInit(out totalSpace);

        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.GetTotalSpaceSize(out totalSpace, in pathNormalized);
    }

    public static Result OpenFile(this IFileSystem fs, ref UniqueRef<IFile> file, string path, OpenMode mode)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.OpenFile(ref file, in pathNormalized, mode);
    }

    public static Result OpenDirectory(this IFileSystem fs, ref UniqueRef<IDirectory> directory, string path, OpenDirectoryMode mode)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.OpenDirectory(ref directory, in pathNormalized, mode);
    }

    public static Result GetFileTimeStampRaw(this IFileSystem fs, out FileTimeStampRaw timeStamp, string path)
    {
        UnsafeHelpers.SkipParamInit(out timeStamp);

        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.GetFileTimeStampRaw(out timeStamp, in pathNormalized);
    }

    public static Result QueryEntry(this IFileSystem fs, Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, string path)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.QueryEntry(outBuffer, inBuffer, queryId, in pathNormalized);
    }

    public static Result CreateDirectory(this IAttributeFileSystem fs, string path, NxFileAttributes archiveAttribute)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.CreateDirectory(in pathNormalized, archiveAttribute);
    }

    public static Result GetFileAttributes(this IAttributeFileSystem fs, out NxFileAttributes attributes, string path)
    {
        UnsafeHelpers.SkipParamInit(out attributes);

        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.GetFileAttributes(out attributes, in pathNormalized);
    }

    public static Result SetFileAttributes(this IAttributeFileSystem fs, string path, NxFileAttributes attributes)
    {
        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.SetFileAttributes(in pathNormalized, attributes);
    }

    public static Result GetFileSize(this IAttributeFileSystem fs, out long fileSize, string path)
    {
        UnsafeHelpers.SkipParamInit(out fileSize);

        using var pathNormalized = new Path();
        Result res = SetUpPath(ref pathNormalized.Ref(), path);
        if (res.IsFailure()) return res.Miss();

        return fs.GetFileSize(out fileSize, in pathNormalized);
    }
}