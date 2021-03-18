using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public static class FileSystemExtensions
    {
        public static Result CopyDirectory(this IFileSystem sourceFs, IFileSystem destFs, string sourcePath, string destPath,
            IProgressReport logger = null, CreateFileOptions options = CreateFileOptions.None)
        {
            Result rc;

            foreach (DirectoryEntryEx entry in sourceFs.EnumerateEntries(sourcePath, "*", SearchOptions.Default))
            {
                string subSrcPath = PathTools.Normalize(PathTools.Combine(sourcePath, entry.Name));
                string subDstPath = PathTools.Normalize(PathTools.Combine(destPath, entry.Name));

                if (entry.Type == DirectoryEntryType.Directory)
                {
                    destFs.EnsureDirectoryExists(subDstPath);

                    rc = sourceFs.CopyDirectory(destFs, subSrcPath, subDstPath, logger, options);
                    if (rc.IsFailure()) return rc;
                }

                if (entry.Type == DirectoryEntryType.File)
                {
                    destFs.CreateOrOverwriteFile(subDstPath, entry.Size, options);

                    rc = sourceFs.OpenFile(out IFile srcFile, subSrcPath.ToU8Span(), OpenMode.Read);
                    if (rc.IsFailure()) return rc;

                    using (srcFile)
                    {
                        rc = destFs.OpenFile(out IFile dstFile, subDstPath.ToU8Span(), OpenMode.Write | OpenMode.AllowAppend);
                        if (rc.IsFailure()) return rc;

                        using (dstFile)
                        {
                            logger?.LogMessage(subSrcPath);
                            srcFile.CopyTo(dstFile, logger);
                        }
                    }
                }
            }

            return Result.Success;
        }

        public static void Extract(this IFileSystem source, string destinationPath, IProgressReport logger = null)
        {
            var destFs = new LocalFileSystem(destinationPath);

            source.CopyDirectory(destFs, "/", "/", logger);
        }

        public static IEnumerable<DirectoryEntryEx> EnumerateEntries(this IFileSystem fileSystem)
        {
            return fileSystem.EnumerateEntries("/", "*");
        }

        public static IEnumerable<DirectoryEntryEx> EnumerateEntries(this IFileSystem fileSystem, string path, string searchPattern)
        {
            return fileSystem.EnumerateEntries(path, searchPattern, SearchOptions.RecurseSubdirectories);
        }

        public static IEnumerable<DirectoryEntryEx> EnumerateEntries(this IFileSystem fileSystem, string searchPattern, SearchOptions searchOptions)
        {
            return EnumerateEntries(fileSystem, "/", searchPattern, searchOptions);
        }

        public static IEnumerable<DirectoryEntryEx> EnumerateEntries(this IFileSystem fileSystem, string path, string searchPattern, SearchOptions searchOptions)
        {
            bool ignoreCase = searchOptions.HasFlag(SearchOptions.CaseInsensitive);
            bool recurse = searchOptions.HasFlag(SearchOptions.RecurseSubdirectories);

            IFileSystem fs = fileSystem;

            fileSystem.OpenDirectory(out IDirectory directory, path.ToU8Span(), OpenDirectoryMode.All).ThrowIfFailure();

            while (true)
            {
                Unsafe.SkipInit(out DirectoryEntry dirEntry);

                directory.Read(out long entriesRead, SpanHelpers.AsSpan(ref dirEntry)).ThrowIfFailure();
                if (entriesRead == 0) break;

                DirectoryEntryEx entry = GetDirectoryEntryEx(ref dirEntry, path);

                if (PathTools.MatchesPattern(searchPattern, entry.Name, ignoreCase))
                {
                    yield return entry;
                }

                if (entry.Type != DirectoryEntryType.Directory || !recurse) continue;

                IEnumerable<DirectoryEntryEx> subEntries =
                    fs.EnumerateEntries(PathTools.Combine(path, entry.Name), searchPattern,
                        searchOptions);

                foreach (DirectoryEntryEx subEntry in subEntries)
                {
                    yield return subEntry;
                }
            }
        }

        internal static DirectoryEntryEx GetDirectoryEntryEx(ref DirectoryEntry entry, string parentPath)
        {
            string name = StringUtils.Utf8ZToString(entry.Name);
            string path = PathTools.Combine(parentPath, name);

            var entryEx = new DirectoryEntryEx(name, path, entry.Type, entry.Size);
            entryEx.Attributes = entry.Attributes;

            return entryEx;
        }

        public static void CopyTo(this IFile file, IFile dest, IProgressReport logger = null)
        {
            const int bufferSize = 0x8000;

            file.GetSize(out long fileSize).ThrowIfFailure();

            logger?.SetTotal(fileSize);

            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                long inOffset = 0;

                // todo: use result for loop condition
                while (true)
                {
                    file.Read(out long bytesRead, inOffset, buffer).ThrowIfFailure();
                    if (bytesRead == 0) break;

                    dest.Write(inOffset, buffer.AsSpan(0, (int)bytesRead)).ThrowIfFailure();
                    inOffset += bytesRead;
                    logger?.ReportAdd(bytesRead);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                logger?.SetTotal(0);
            }
        }

        public static IStorage AsStorage(this IFile file) => new FileStorage(file);
        public static Stream AsStream(this IFile file) => new NxFileStream(file, true);
        public static Stream AsStream(this IFile file, OpenMode mode, bool keepOpen) => new NxFileStream(file, mode, keepOpen);

        public static IFile AsIFile(this Stream stream, OpenMode mode) => new StreamFile(stream, mode);

        public static int GetEntryCount(this IFileSystem fs, OpenDirectoryMode mode)
        {
            return GetEntryCountRecursive(fs, "/", mode);
        }

        public static int GetEntryCountRecursive(this IFileSystem fs, string path, OpenDirectoryMode mode)
        {
            int count = 0;

            foreach (DirectoryEntryEx entry in fs.EnumerateEntries(path, "*"))
            {
                if (entry.Type == DirectoryEntryType.Directory && (mode & OpenDirectoryMode.Directory) != 0 ||
                    entry.Type == DirectoryEntryType.File && (mode & OpenDirectoryMode.File) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        public static NxFileAttributes ToNxAttributes(this FileAttributes attributes)
        {
            return (NxFileAttributes)(((int)attributes >> 4) & 3);
        }

        public static FileAttributes ToFatAttributes(this NxFileAttributes attributes)
        {
            return (FileAttributes)(((int)attributes & 3) << 4);
        }

        public static FileAttributes ApplyNxAttributes(this FileAttributes attributes, NxFileAttributes nxAttributes)
        {
            // The only 2 bits from FileAttributes that are used in NxFileAttributes
            const int mask = 3 << 4;

            FileAttributes oldAttributes = attributes & (FileAttributes)mask;
            return oldAttributes | nxAttributes.ToFatAttributes();
        }

        public static void SetConcatenationFileAttribute(this IFileSystem fs, string path)
        {
            fs.QueryEntry(Span<byte>.Empty, Span<byte>.Empty, QueryId.MakeConcatFile, path.ToU8Span());
        }

        public static void CleanDirectoryRecursivelyGeneric(IFileSystem fileSystem, string path)
        {
            IFileSystem fs = fileSystem;

            foreach (DirectoryEntryEx entry in fileSystem.EnumerateEntries(path, "*", SearchOptions.Default))
            {
                string subPath = PathTools.Combine(path, entry.Name);

                if (entry.Type == DirectoryEntryType.Directory)
                {
                    CleanDirectoryRecursivelyGeneric(fileSystem, subPath);
                    fs.DeleteDirectory(subPath.ToU8Span());
                }
                else if (entry.Type == DirectoryEntryType.File)
                {
                    fs.DeleteFile(subPath.ToU8Span());
                }
            }
        }

        public static Result Read(this IFile file, out long bytesRead, long offset, Span<byte> destination)
        {
            return file.Read(out bytesRead, offset, destination, ReadOption.None);
        }

        public static Result Write(this IFile file, long offset, ReadOnlySpan<byte> source)
        {
            return file.Write(offset, source, WriteOption.None);
        }

        public static bool DirectoryExists(this IFileSystem fs, string path)
        {
            Result rc = fs.GetEntryType(out DirectoryEntryType type, path.ToU8Span());

            return (rc.IsSuccess() && type == DirectoryEntryType.Directory);
        }

        public static bool FileExists(this IFileSystem fs, string path)
        {
            Result rc = fs.GetEntryType(out DirectoryEntryType type, path.ToU8Span());

            return (rc.IsSuccess() && type == DirectoryEntryType.File);
        }

        public static Result EnsureDirectoryExists(this IFileSystem fs, string path)
        {
            path = PathTools.Normalize(path);
            if (fs.DirectoryExists(path)) return Result.Success;

            // Find the first subdirectory in the chain that doesn't exist
            int i;
            for (i = path.Length - 1; i > 0; i--)
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

                    Result rc = fs.CreateDirectory(subPath.ToU8Span());
                    if (rc.IsFailure()) return rc;
                }
            }

            return fs.CreateDirectory(path.ToU8Span());
        }

        public static void CreateOrOverwriteFile(this IFileSystem fs, string path, long size)
        {
            fs.CreateOrOverwriteFile(path, size, CreateFileOptions.None);
        }

        public static void CreateOrOverwriteFile(this IFileSystem fs, string path, long size, CreateFileOptions options)
        {
            path = PathTools.Normalize(path);

            if (fs.FileExists(path)) fs.DeleteFile(path.ToU8Span());

            fs.CreateFile(path.ToU8Span(), size, CreateFileOptions.None);
        }
    }

    [Flags]
    public enum SearchOptions
    {
        Default = 0,
        RecurseSubdirectories = 1 << 0,
        CaseInsensitive = 1 << 1
    }
}
