using System;
using System.Buffers;
using System.Collections.Generic;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.FsSystem;

namespace LibHac.Tools.Fs;

public static class FileSystemClientUtils
{
    public static Result CopyDirectory(this FileSystemClient fs, string sourcePath, string destPath,
        CreateFileOptions options = CreateFileOptions.None, IProgressReport logger = null)
    {
        Result res = fs.OpenDirectory(out DirectoryHandle sourceHandle, sourcePath.ToU8Span(), OpenDirectoryMode.All);
        if (res.IsFailure()) return res.Miss();

        try
        {
            foreach (DirectoryEntryEx entry in fs.EnumerateEntries(sourcePath, "*", SearchOptions.Default))
            {
                string subSrcPath = PathTools.Normalize(PathTools.Combine(sourcePath, entry.Name));
                string subDstPath = PathTools.Normalize(PathTools.Combine(destPath, entry.Name));

                if (entry.Type == DirectoryEntryType.Directory)
                {
                    fs.EnsureDirectoryExists(subDstPath);

                    res = fs.CopyDirectory(subSrcPath, subDstPath, options, logger);
                    if (res.IsFailure()) return res.Miss();
                }

                if (entry.Type == DirectoryEntryType.File)
                {
                    logger?.LogMessage(subSrcPath);
                    fs.CreateOrOverwriteFile(subDstPath, entry.Size, options);

                    res = fs.CopyFile(subSrcPath, subDstPath, logger);
                    if (res.IsFailure()) return res.Miss();
                }
            }
        }
        finally
        {
            if (sourceHandle.IsValid)
                fs.CloseDirectory(sourceHandle);
        }

        return Result.Success;
    }

    public static Result CopyFile(this FileSystemClient fs, string sourcePath, string destPath, IProgressReport logger = null)
    {
        Result res = fs.OpenFile(out FileHandle sourceHandle, sourcePath.ToU8Span(), OpenMode.Read);
        if (res.IsFailure()) return res.Miss();

        try
        {
            res = fs.OpenFile(out FileHandle destHandle, destPath.ToU8Span(),
                OpenMode.Write | OpenMode.AllowAppend);
            if (res.IsFailure()) return res.Miss();

            try
            {
                const int maxBufferSize = 0x10000;

                res = fs.GetFileSize(out long fileSize, sourceHandle);
                if (res.IsFailure()) return res.Miss();

                int bufferSize = (int)Math.Min(maxBufferSize, fileSize);

                logger?.SetTotal(fileSize);

                byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                try
                {
                    for (long offset = 0; offset < fileSize; offset += bufferSize)
                    {
                        int toRead = (int)Math.Min(fileSize - offset, bufferSize);
                        Span<byte> buf = buffer.AsSpan(0, toRead);

                        res = fs.ReadFile(out long _, sourceHandle, offset, buf);
                        if (res.IsFailure()) return res.Miss();

                        res = fs.WriteFile(destHandle, offset, buf, WriteOption.None);
                        if (res.IsFailure()) return res.Miss();

                        logger?.ReportAdd(toRead);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    logger?.SetTotal(0);
                }

                res = fs.FlushFile(destHandle);
                if (res.IsFailure()) return res.Miss();
            }
            finally
            {
                if (destHandle.IsValid)
                    fs.CloseFile(destHandle);
            }
        }
        finally
        {
            if (sourceHandle.IsValid)
                fs.CloseFile(sourceHandle);
        }

        return Result.Success;
    }

    public static IEnumerable<DirectoryEntryEx> EnumerateEntries(this FileSystemClient fs, string path)
    {
        return fs.EnumerateEntries(path, "*");
    }

    public static IEnumerable<DirectoryEntryEx> EnumerateEntries(this FileSystemClient fs, string path, string searchPattern)
    {
        return fs.EnumerateEntries(path, searchPattern, SearchOptions.RecurseSubdirectories);
    }

    public static IEnumerable<DirectoryEntryEx> EnumerateEntries(this FileSystemClient fs, string path, string searchPattern, SearchOptions searchOptions)
    {
        bool ignoreCase = searchOptions.HasFlag(SearchOptions.CaseInsensitive);
        bool recurse = searchOptions.HasFlag(SearchOptions.RecurseSubdirectories);

        fs.OpenDirectory(out DirectoryHandle sourceHandle, path.ToU8Span(), OpenDirectoryMode.All).ThrowIfFailure();

        try
        {
            while (true)
            {
                DirectoryEntry dirEntry = default;

                fs.ReadDirectory(out long entriesRead, SpanHelpers.AsSpan(ref dirEntry), sourceHandle);
                if (entriesRead == 0) break;

                DirectoryEntryEx entry = FileSystemExtensions.GetDirectoryEntryEx(ref dirEntry, path);

                if (PathTools.MatchesPattern(searchPattern, entry.Name, ignoreCase))
                {
                    yield return entry;
                }

                if (entry.Type != DirectoryEntryType.Directory || !recurse) continue;

                IEnumerable<DirectoryEntryEx> subEntries =
                    fs.EnumerateEntries(PathTools.Combine(path, entry.Name), searchPattern, searchOptions);

                foreach (DirectoryEntryEx subEntry in subEntries)
                {
                    yield return subEntry;
                }
            }
        }
        finally
        {
            if (sourceHandle.IsValid)
                fs.CloseDirectory(sourceHandle);
        }
    }

    public static bool DirectoryExists(this FileSystemClient fs, string path)
    {
        Result res = fs.GetEntryType(out DirectoryEntryType type, path.ToU8Span());

        return (res.IsSuccess() && type == DirectoryEntryType.Directory);
    }

    public static bool FileExists(this FileSystemClient fs, string path)
    {
        Result res = fs.GetEntryType(out DirectoryEntryType type, path.ToU8Span());

        return (res.IsSuccess() && type == DirectoryEntryType.File);
    }

    public static void EnsureDirectoryExists(this FileSystemClient fs, string path)
    {
        path = PathTools.Normalize(path);
        if (fs.DirectoryExists(path)) return;

        PathTools.GetMountNameLength(path, out int mountNameLength).ThrowIfFailure();

        // Find the first subdirectory in the path that doesn't exist
        int i;
        for (i = path.Length - 1; i > mountNameLength + 2; i--)
        {
            if (path[i] == '/')
            {
                string subPath = path.Substring(0, i);

                if (fs.DirectoryExists(subPath))
                {
                    break;
                }
            }
        }

        // path[i] will be a '/', so skip that character
        i++;

        // loop until `path.Length - 1` so CreateDirectory won't be called multiple
        // times on path if the last character in the path is a '/'
        for (; i < path.Length - 1; i++)
        {
            if (path[i] == '/')
            {
                string subPath = path.Substring(0, i);

                fs.CreateDirectory(subPath.ToU8Span());
            }
        }

        fs.CreateDirectory(path.ToU8Span());
    }

    public static Result CreateOrOverwriteFile(this FileSystemClient fs, string path, long size)
    {
        return fs.CreateOrOverwriteFile(path, size, CreateFileOptions.None);
    }

    public static Result CreateOrOverwriteFile(this FileSystemClient fs, string path, long size, CreateFileOptions options)
    {
        path = PathTools.Normalize(path);
        var u8Path = path.ToU8Span();

        if (fs.FileExists(path))
        {
            Result res = fs.DeleteFile(u8Path);
            if (res.IsFailure()) return res.Miss();
        }

        return fs.CreateFile(u8Path, size, CreateFileOptions.None);
    }
}