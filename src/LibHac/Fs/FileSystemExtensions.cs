using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

namespace LibHac.Fs
{
    public static class FileSystemExtensions
    {
        public static Result CopyDirectory(this IDirectory source, IDirectory dest, IProgressReport logger = null, CreateFileOptions options = CreateFileOptions.None)
        {
            IFileSystem sourceFs = source.ParentFileSystem;
            IFileSystem destFs = dest.ParentFileSystem;
            Result rc;

            foreach (DirectoryEntry entry in source.Read())
            {
                string subSrcPath = PathTools.Normalize(PathTools.Combine(source.FullPath, entry.Name));
                string subDstPath = PathTools.Normalize(PathTools.Combine(dest.FullPath, entry.Name));

                if (entry.Type == DirectoryEntryType.Directory)
                {
                    destFs.EnsureDirectoryExists(subDstPath);

                    rc = sourceFs.OpenDirectory(out IDirectory subSrcDir, subSrcPath, OpenDirectoryMode.All);
                    if (rc.IsFailure()) return rc;

                    rc = destFs.OpenDirectory(out IDirectory subDstDir, subDstPath, OpenDirectoryMode.All);
                    if (rc.IsFailure()) return rc;

                    rc = subSrcDir.CopyDirectory(subDstDir, logger, options);
                    if (rc.IsFailure()) return rc;
                }

                if (entry.Type == DirectoryEntryType.File)
                {
                    destFs.CreateOrOverwriteFile(subDstPath, entry.Size, options);

                    rc = sourceFs.OpenFile(out IFile srcFile, subSrcPath, OpenMode.Read);
                    if (rc.IsFailure()) return rc;

                    using (srcFile)
                    {
                        rc = destFs.OpenFile(out IFile dstFile, subDstPath, OpenMode.Write | OpenMode.AllowAppend);
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

        public static void CopyFileSystem(this IFileSystem source, IFileSystem dest, IProgressReport logger = null, CreateFileOptions options = CreateFileOptions.None)
        {
            source.OpenDirectory(out IDirectory sourceRoot, "/", OpenDirectoryMode.All).ThrowIfFailure();
            dest.OpenDirectory(out IDirectory destRoot, "/", OpenDirectoryMode.All).ThrowIfFailure();

            sourceRoot.CopyDirectory(destRoot, logger, options).ThrowIfFailure();
        }

        public static void Extract(this IFileSystem source, string destinationPath, IProgressReport logger = null)
        {
            var destFs = new LocalFileSystem(destinationPath);

            source.CopyFileSystem(destFs, logger);
        }

        public static IEnumerable<DirectoryEntry> EnumerateEntries(this IFileSystem fileSystem)
        {
            return fileSystem.EnumerateEntries("*");
        }

        public static IEnumerable<DirectoryEntry> EnumerateEntries(this IFileSystem fileSystem, string searchPattern)
        {
            return fileSystem.EnumerateEntries(searchPattern, SearchOptions.RecurseSubdirectories);
        }

        public static IEnumerable<DirectoryEntry> EnumerateEntries(this IFileSystem fileSystem, string searchPattern, SearchOptions searchOptions)
        {
            fileSystem.OpenDirectory(out IDirectory rootDir, "/", OpenDirectoryMode.All).ThrowIfFailure();

            return rootDir.EnumerateEntries(searchPattern, searchOptions);
        }

        public static IEnumerable<DirectoryEntry> EnumerateEntries(this IDirectory directory)
        {
            return directory.EnumerateEntries("*", SearchOptions.Default);
        }

        public static IEnumerable<DirectoryEntry> EnumerateEntries(this IDirectory directory, string searchPattern, SearchOptions searchOptions)
        {
            bool ignoreCase = searchOptions.HasFlag(SearchOptions.CaseInsensitive);
            bool recurse = searchOptions.HasFlag(SearchOptions.RecurseSubdirectories);

            IFileSystem fs = directory.ParentFileSystem;

            foreach (DirectoryEntry entry in directory.Read())
            {
                if (PathTools.MatchesPattern(searchPattern, entry.Name, ignoreCase))
                {
                    yield return entry;
                }

                if (entry.Type != DirectoryEntryType.Directory || !recurse) continue;

                fs.OpenDirectory(out IDirectory subDir, PathTools.Combine(directory.FullPath, entry.Name), OpenDirectoryMode.All).ThrowIfFailure();

                foreach (DirectoryEntry subEntry in subDir.EnumerateEntries(searchPattern, searchOptions))
                {
                    yield return subEntry;
                }
            }
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
        public static Stream AsStream(this IFile file, bool keepOpen) => new NxFileStream(file, keepOpen);

        public static IFile AsIFile(this Stream stream, OpenMode mode) => new StreamFile(stream, mode);

        public static int GetEntryCount(this IFileSystem fs, OpenDirectoryMode mode)
        {
            fs.OpenDirectory(out IDirectory rootDir, "/", OpenDirectoryMode.All).ThrowIfFailure();

            return rootDir.GetEntryCountRecursive(mode);
        }

        public static int GetEntryCountRecursive(this IDirectory directory, OpenDirectoryMode mode)
        {
            int count = 0;

            foreach (DirectoryEntry entry in directory.EnumerateEntries())
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

        public static FileAttributes ApplyNxAttributes(this FileAttributes attributes, NxFileAttributes nxAttributes)
        {
            var nxAttributeBits = (FileAttributes)(((int)nxAttributes & 3) << 4);
            return attributes | nxAttributeBits;
        }

        public static void SetConcatenationFileAttribute(this IFileSystem fs, string path)
        {
            fs.QueryEntry(Span<byte>.Empty, Span<byte>.Empty, QueryId.MakeConcatFile, path);
        }

        public static void CleanDirectoryRecursivelyGeneric(IDirectory directory)
        {
            IFileSystem fs = directory.ParentFileSystem;

            foreach (DirectoryEntry entry in directory.Read())
            {
                string subPath = PathTools.Combine(directory.FullPath, entry.Name);

                if (entry.Type == DirectoryEntryType.Directory)
                {
                    fs.OpenDirectory(out IDirectory subDir, subPath, OpenDirectoryMode.All).ThrowIfFailure();

                    CleanDirectoryRecursivelyGeneric(subDir);
                    fs.DeleteDirectory(subPath);
                }
                else if (entry.Type == DirectoryEntryType.File)
                {
                    fs.DeleteFile(subPath);
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
            Result rc = fs.GetEntryType(out DirectoryEntryType type, path);

            return (rc.IsSuccess() && type == DirectoryEntryType.Directory);
        }

        public static bool FileExists(this IFileSystem fs, string path)
        {
            Result rc = fs.GetEntryType(out DirectoryEntryType type, path);

            return (rc.IsSuccess() && type == DirectoryEntryType.File);
        }

        public static void EnsureDirectoryExists(this IFileSystem fs, string path)
        {
            path = PathTools.Normalize(path);
            if (fs.DirectoryExists(path)) return;

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

                    fs.CreateDirectory(subPath);
                }
            }

            fs.CreateDirectory(path);
        }

        public static void CreateOrOverwriteFile(this IFileSystem fs, string path, long size)
        {
            fs.CreateOrOverwriteFile(path, size, CreateFileOptions.None);
        }

        public static void CreateOrOverwriteFile(this IFileSystem fs, string path, long size, CreateFileOptions options)
        {
            path = PathTools.Normalize(path);

            if (fs.FileExists(path)) fs.DeleteFile(path);

            fs.CreateFile(path, size, CreateFileOptions.None);
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
