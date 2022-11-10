using System;
using System.Buffers;
using LibHac;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;

namespace hactoolnet;

public static class FsUtils
{
    public static Result CopyDirectoryWithProgress(FileSystemClient fs, U8Span sourcePath, U8Span destPath,
        CreateFileOptions options = CreateFileOptions.None, IProgressReport logger = null)
    {
        try
        {
            logger?.SetTotal(GetTotalSize(fs, sourcePath));

            return CopyDirectoryWithProgressInternal(fs, sourcePath, destPath, options, logger);
        }
        finally
        {
            logger?.SetTotal(0);
        }
    }

    private static Result CopyDirectoryWithProgressInternal(FileSystemClient fs, U8Span sourcePath, U8Span destPath,
        CreateFileOptions options, IProgressReport logger)
    {
        string sourcePathStr = sourcePath.ToString();
        string destPathStr = destPath.ToString();

        Result res = fs.OpenDirectory(out DirectoryHandle sourceHandle, sourcePath, OpenDirectoryMode.All);
        if (res.IsFailure()) return res.Miss();

        try
        {
            foreach (DirectoryEntryEx entry in fs.EnumerateEntries(sourcePathStr, "*", SearchOptions.Default))
            {
                string subSrcPath = PathTools.Normalize(PathTools.Combine(sourcePathStr, entry.Name));
                string subDstPath = PathTools.Normalize(PathTools.Combine(destPathStr, entry.Name));

                if (entry.Type == DirectoryEntryType.Directory)
                {
                    fs.EnsureDirectoryExists(subDstPath);

                    res = CopyDirectoryWithProgressInternal(fs, subSrcPath.ToU8Span(), subDstPath.ToU8Span(), options, logger);
                    if (res.IsFailure()) return res.Miss();
                }

                if (entry.Type == DirectoryEntryType.File)
                {
                    logger?.LogMessage(subSrcPath);

                    res = fs.CreateOrOverwriteFile(subDstPath, entry.Size, options);
                    if (res.IsFailure()) return res.Miss();

                    res = CopyFileWithProgress(fs, subSrcPath.ToU8Span(), subDstPath.ToU8Span(), logger);
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

    public static long GetTotalSize(FileSystemClient fs, U8Span path, string searchPattern = "*")
    {
        long size = 0;

        foreach (DirectoryEntryEx entry in fs.EnumerateEntries(path.ToString(), searchPattern))
        {
            size += entry.Size;
        }

        return size;
    }

    public static Result CopyFileWithProgress(FileSystemClient fs, U8Span sourcePath, U8Span destPath, IProgressReport logger = null)
    {
        Result res = fs.OpenFile(out FileHandle sourceHandle, sourcePath, OpenMode.Read);
        if (res.IsFailure()) return res.Miss();

        try
        {
            res = fs.OpenFile(out FileHandle destHandle, destPath, OpenMode.Write | OpenMode.AllowAppend);
            if (res.IsFailure()) return res.Miss();

            try
            {
                const int maxBufferSize = 1024 * 1024;

                res = fs.GetFileSize(out long fileSize, sourceHandle);
                if (res.IsFailure()) return res.Miss();

                int bufferSize = (int)Math.Min(maxBufferSize, fileSize);

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
}