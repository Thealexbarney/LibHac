using System;
using System.Buffers;
using LibHac;
using LibHac.Fs;
using LibHac.FsClient;

namespace hactoolnet
{
    public static class FsUtils
    {
        public static void CopyDirectoryWithProgress(FileSystemManager fs, string sourcePath, string destPath,
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

        private static void CopyDirectoryWithProgressInternal(FileSystemManager fs, string sourcePath, string destPath,
            CreateFileOptions options, IProgressReport logger)
        {
            using (DirectoryHandle sourceHandle = fs.OpenDirectory(sourcePath, OpenDirectoryMode.All))
            {
                foreach (DirectoryEntry entry in fs.ReadDirectory(sourceHandle))
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

        public static long GetTotalSize(FileSystemManager fs, string path, string searchPattern = "*")
        {
            long size = 0;

            foreach (DirectoryEntry entry in fs.EnumerateEntries(path, searchPattern))
            {
                size += entry.Size;
            }

            return size;
        }

        public static void CopyFileWithProgress(FileSystemManager fs, string sourcePath, string destPath, IProgressReport logger = null)
        {
            using (FileHandle sourceHandle = fs.OpenFile(sourcePath, OpenMode.Read))
            using (FileHandle destHandle = fs.OpenFile(destPath, OpenMode.Write | OpenMode.AllowAppend))
            {
                const int maxBufferSize = 1024 * 1024;

                long fileSize = fs.GetFileSize(sourceHandle);
                int bufferSize = (int)Math.Min(maxBufferSize, fileSize);

                byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                try
                {
                    for (long offset = 0; offset < fileSize; offset += bufferSize)
                    {
                        int toRead = (int)Math.Min(fileSize - offset, bufferSize);
                        Span<byte> buf = buffer.AsSpan(0, toRead);

                        fs.ReadFile(sourceHandle, buf, offset);
                        fs.WriteFile(destHandle, buf, offset);

                        logger?.ReportAdd(toRead);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                fs.FlushFile(destHandle);
            }
        }
    }
}
