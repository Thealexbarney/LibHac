using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

#if !NETFRAMEWORK
using System.IO.Enumeration;
#endif

namespace LibHac.Fs
{
    public static class FileSystemExtensions
    {
        public static void CopyDirectory(this IDirectory source, IDirectory dest, IProgressReport logger = null, CreateFileOptions options = CreateFileOptions.None)
        {
            IFileSystem sourceFs = source.ParentFileSystem;
            IFileSystem destFs = dest.ParentFileSystem;

            foreach (DirectoryEntry entry in source.Read())
            {
                string subSrcPath = PathTools.Normalize(PathTools.Combine(source.FullPath, entry.Name));
                string subDstPath = PathTools.Normalize(PathTools.Combine(dest.FullPath, entry.Name));

                if (entry.Type == DirectoryEntryType.Directory)
                {
                    destFs.CreateDirectory(subDstPath);
                    IDirectory subSrcDir = sourceFs.OpenDirectory(subSrcPath, OpenDirectoryMode.All);
                    IDirectory subDstDir = destFs.OpenDirectory(subDstPath, OpenDirectoryMode.All);

                    subSrcDir.CopyDirectory(subDstDir, logger, options);
                }

                if (entry.Type == DirectoryEntryType.File)
                {
                    destFs.CreateFile(subDstPath, entry.Size, options);

                    using (IFile srcFile = sourceFs.OpenFile(subSrcPath, OpenMode.Read))
                    using (IFile dstFile = destFs.OpenFile(subDstPath, OpenMode.Write | OpenMode.Append))
                    {
                        logger?.LogMessage(subSrcPath);
                        srcFile.CopyTo(dstFile, logger);
                    }
                }
            }
        }

        public static void CopyFileSystem(this IFileSystem source, IFileSystem dest, IProgressReport logger = null, CreateFileOptions options = CreateFileOptions.None)
        {
            IDirectory sourceRoot = source.OpenDirectory("/", OpenDirectoryMode.All);
            IDirectory destRoot = dest.OpenDirectory("/", OpenDirectoryMode.All);

            sourceRoot.CopyDirectory(destRoot, logger, options);
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
            return fileSystem.OpenDirectory("/", OpenDirectoryMode.All).EnumerateEntries(searchPattern, searchOptions);
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
                if (MatchesPattern(searchPattern, entry.Name, ignoreCase))
                {
                    yield return entry;
                }

                if (entry.Type != DirectoryEntryType.Directory || !recurse) continue;

                IDirectory subDir = fs.OpenDirectory(PathTools.Combine(directory.FullPath, entry.Name), OpenDirectoryMode.All);

                foreach (DirectoryEntry subEntry in subDir.EnumerateEntries(searchPattern, searchOptions))
                {
                    yield return subEntry;
                }
            }
        }

        public static void CopyTo(this IFile file, IFile dest, IProgressReport logger = null)
        {
            const int bufferSize = 0x8000;
            logger?.SetTotal(file.GetSize());

            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                long inOffset = 0;

                int bytesRead;
                while ((bytesRead = file.Read(buffer, inOffset)) != 0)
                {
                    dest.Write(buffer.AsSpan(0, bytesRead), inOffset);
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
            return fs.OpenDirectory("/", OpenDirectoryMode.All).GetEntryCountRecursive(mode);
        }

        public static int GetEntryCountRecursive(this IDirectory directory, OpenDirectoryMode mode)
        {
            int count = 0;

            foreach (DirectoryEntry entry in directory.EnumerateEntries())
            {
                if (entry.Type == DirectoryEntryType.Directory && (mode & OpenDirectoryMode.Directories) != 0 ||
                    entry.Type == DirectoryEntryType.File && (mode & OpenDirectoryMode.Files) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        public static bool MatchesPattern(string searchPattern, string name, bool ignoreCase)
        {
#if NETFRAMEWORK
            return Compatibility.FileSystemName.MatchesSimpleExpression(searchPattern.AsSpan(),
                name.AsSpan(), ignoreCase);
#else
            return FileSystemName.MatchesSimpleExpression(searchPattern.AsSpan(),
                name.AsSpan(), ignoreCase);
#endif
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
            fs.QueryEntry(Span<byte>.Empty, Span<byte>.Empty, path, QueryId.MakeConcatFile);
        }

        public static void CleanDirectoryRecursivelyGeneric(IDirectory directory)
        {
            IFileSystem fs = directory.ParentFileSystem;

            foreach (DirectoryEntry entry in directory.Read())
            {
                string subPath = PathTools.Combine(directory.FullPath, entry.Name);

                if (entry.Type == DirectoryEntryType.Directory)
                {
                    IDirectory subDir = fs.OpenDirectory(subPath, OpenDirectoryMode.All);

                    CleanDirectoryRecursivelyGeneric(subDir);
                    fs.DeleteDirectory(subPath);
                }
                else if (entry.Type == DirectoryEntryType.File)
                {
                    fs.DeleteFile(subPath);
                }
            }
        }

        public static int Read(this IFile file, Span<byte> destination, long offset)
        {
            return file.Read(destination, offset, ReadOption.None);
        }

        public static void Write(this IFile file, ReadOnlySpan<byte> source, long offset)
        {
            file.Write(source, offset, WriteOption.None);
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
