using System;
using System.Buffers;
using System.Collections.Generic;
using LibHac.Fs.Accessors;

namespace LibHac.Fs
{
    public static class FileSystemManagerUtils
    {
        public static void CopyDirectory(this FileSystemManager fs, string sourcePath, string destPath,
            CreateFileOptions options = CreateFileOptions.None, IProgressReport logger = null)
        {
            using (DirectoryHandle sourceHandle = fs.OpenDirectory(sourcePath, OpenDirectoryMode.All))
            {
                foreach (DirectoryEntry entry in fs.ReadDirectory(sourceHandle))
                {
                    string subSrcPath = PathTools.Normalize(PathTools.Combine(sourcePath, entry.Name));
                    string subDstPath = PathTools.Normalize(PathTools.Combine(destPath, entry.Name));

                    if (entry.Type == DirectoryEntryType.Directory)
                    {
                        fs.CreateDirectory(subDstPath);

                        fs.CopyDirectory(subSrcPath, subDstPath, options, logger);
                    }

                    if (entry.Type == DirectoryEntryType.File)
                    {
                        logger?.LogMessage(subSrcPath);
                        fs.CreateFile(subDstPath, entry.Size, options);

                        fs.CopyFile(subSrcPath, subDstPath, logger);
                    }
                }
            }
        }

        public static void CopyFile(this FileSystemManager fs, string sourcePath, string destPath, IProgressReport logger = null)
        {
            using (FileHandle sourceHandle = fs.OpenFile(sourcePath, OpenMode.Read))
            using (FileHandle destHandle = fs.OpenFile(destPath, OpenMode.Write | OpenMode.Append))
            {
                const int maxBufferSize = 0x10000;

                long fileSize = fs.GetFileSize(sourceHandle);
                int bufferSize = (int)Math.Min(maxBufferSize, fileSize);

                logger?.SetTotal(fileSize);

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
                    logger?.SetTotal(0);
                }

                fs.FlushFile(destHandle);
            }
        }

        public static IEnumerable<DirectoryEntry> EnumerateEntries(this FileSystemManager fs, string path)
        {
            return fs.EnumerateEntries(path, "*");
        }

        public static IEnumerable<DirectoryEntry> EnumerateEntries(this FileSystemManager fs, string path, string searchPattern)
        {
            return fs.EnumerateEntries(path, searchPattern, SearchOptions.RecurseSubdirectories);
        }

        public static IEnumerable<DirectoryEntry> EnumerateEntries(this FileSystemManager fs, string path, string searchPattern, SearchOptions searchOptions)
        {
            bool ignoreCase = searchOptions.HasFlag(SearchOptions.CaseInsensitive);
            bool recurse = searchOptions.HasFlag(SearchOptions.RecurseSubdirectories);

            using (DirectoryHandle sourceHandle = fs.OpenDirectory(path, OpenDirectoryMode.All))
            {
                foreach (DirectoryEntry entry in fs.ReadDirectory(sourceHandle))
                {
                    if (PathTools.MatchesPattern(searchPattern, entry.Name, ignoreCase))
                    {
                        yield return entry;
                    }

                    if (entry.Type != DirectoryEntryType.Directory || !recurse) continue;

                    string subPath = PathTools.Normalize(PathTools.Combine(path, entry.Name));

                    IEnumerable<DirectoryEntry> subEntries = fs.EnumerateEntries(subPath, searchPattern, searchOptions);

                    foreach (DirectoryEntry subEntry in subEntries)
                    {
                        subEntry.FullPath = PathTools.Combine(path, subEntry.Name);
                        yield return subEntry;
                    }
                }
            }
        }
    }
}
