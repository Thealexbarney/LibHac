using System;
using System.Buffers;
using LibHac;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace hactoolnet
{
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

            Result rc = fs.OpenDirectory(out DirectoryHandle sourceHandle, sourcePath, OpenDirectoryMode.All);
            if (rc.IsFailure()) return rc;

            try
            {
                foreach (DirectoryEntryEx entry in fs.EnumerateEntries(sourcePathStr, "*", SearchOptions.Default))
                {
                    string subSrcPath = PathTools.Normalize(PathTools.Combine(sourcePathStr, entry.Name));
                    string subDstPath = PathTools.Normalize(PathTools.Combine(destPathStr, entry.Name));

                    if (entry.Type == DirectoryEntryType.Directory)
                    {
                        fs.EnsureDirectoryExists(subDstPath);

                        rc = CopyDirectoryWithProgressInternal(fs, subSrcPath.ToU8Span(), subDstPath.ToU8Span(), options, logger);
                        if (rc.IsFailure()) return rc;
                    }

                    if (entry.Type == DirectoryEntryType.File)
                    {
                        logger?.LogMessage(subSrcPath);

                        rc = fs.CreateOrOverwriteFile(subDstPath, entry.Size, options);
                        if (rc.IsFailure()) return rc;

                        rc = CopyFileWithProgress(fs, subSrcPath.ToU8Span(), subDstPath.ToU8Span(), logger);
                        if (rc.IsFailure()) return rc;
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
            Result rc = fs.OpenFile(out FileHandle sourceHandle, sourcePath, OpenMode.Read);
            if (rc.IsFailure()) return rc;

            try
            {
                rc = fs.OpenFile(out FileHandle destHandle, destPath, OpenMode.Write | OpenMode.AllowAppend);
                if (rc.IsFailure()) return rc;

                try
                {
                    const int maxBufferSize = 1024 * 1024;

                    rc = fs.GetFileSize(out long fileSize, sourceHandle);
                    if (rc.IsFailure()) return rc;

                    int bufferSize = (int)Math.Min(maxBufferSize, fileSize);

                    byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        for (long offset = 0; offset < fileSize; offset += bufferSize)
                        {
                            int toRead = (int)Math.Min(fileSize - offset, bufferSize);
                            Span<byte> buf = buffer.AsSpan(0, toRead);

                            rc = fs.ReadFile(out long _, sourceHandle, offset, buf);
                            if (rc.IsFailure()) return rc;

                            rc = fs.WriteFile(destHandle, offset, buf, WriteOption.None);
                            if (rc.IsFailure()) return rc;

                            logger?.ReportAdd(toRead);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }

                    rc = fs.FlushFile(destHandle);
                    if (rc.IsFailure()) return rc;
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
}
