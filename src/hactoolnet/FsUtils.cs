using System;
using System.Buffers;
using LibHac;
using LibHac.Fs;
using LibHac.FsSystem;

namespace hactoolnet
{
    public static class FsUtils
    {
        public static void CopyDirectoryWithProgress(FileSystemClient fs, string sourcePath, string destPath,
            CreateFileOptions options = CreateFileOptions.None, IProgressReport logger = null)
        {
            try
            {
                logger?.SetTotal(GetTotalSize(fs, sourcePath));

                CopyDirectoryWithProgressInternal(fs, sourcePath, destPath, options, logger);
            }
            finally
            {
                logger?.SetTotal(0);
            }
        }

        private static void CopyDirectoryWithProgressInternal(FileSystemClient fs, string sourcePath, string destPath,
            CreateFileOptions options, IProgressReport logger)
        {
            fs.OpenDirectory(out DirectoryHandle sourceHandle, sourcePath, OpenDirectoryMode.All).ThrowIfFailure();

            using (sourceHandle)
            {
                foreach (DirectoryEntryEx entry in fs.EnumerateEntries(sourcePath, "*", SearchOptions.Default))
                {
                    string subSrcPath = PathTools.Normalize(PathTools.Combine(sourcePath, entry.Name));
                    string subDstPath = PathTools.Normalize(PathTools.Combine(destPath, entry.Name));

                    if (entry.Type == DirectoryEntryType.Directory)
                    {
                        fs.EnsureDirectoryExists(subDstPath);

                        CopyDirectoryWithProgressInternal(fs, subSrcPath, subDstPath, options, logger);
                    }

                    if (entry.Type == DirectoryEntryType.File)
                    {
                        logger?.LogMessage(subSrcPath);
                        fs.CreateOrOverwriteFile(subDstPath, entry.Size, options);

                        CopyFileWithProgress(fs, subSrcPath, subDstPath, logger);
                    }
                }
            }
        }

        public static long GetTotalSize(FileSystemClient fs, string path, string searchPattern = "*")
        {
            long size = 0;

            foreach (DirectoryEntryEx entry in fs.EnumerateEntries(path, searchPattern))
            {
                size += entry.Size;
            }

            return size;
        }

        public static Result CopyFileWithProgress(FileSystemClient fs, string sourcePath, string destPath, IProgressReport logger = null)
        {
            Result rc = fs.OpenFile(out FileHandle sourceHandle, sourcePath, OpenMode.Read);
            if (rc.IsFailure()) return rc;

            using (sourceHandle)
            {
                rc = fs.OpenFile(out FileHandle destHandle, destPath, OpenMode.Write | OpenMode.AllowAppend);
                if (rc.IsFailure()) return rc;

                using (destHandle)
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

                            rc = fs.WriteFile(destHandle, offset, buf);
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
            }

            return Result.Success;
        }
    }
}
