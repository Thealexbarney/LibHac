using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;
using Path = LibHac.Fs.Path;

namespace LibHac.FsSystem;

public static class FileSystemExtensions
{
    public static Result CopyDirectory(this IFileSystem sourceFs, IFileSystem destFs, string sourcePath, string destPath,
        IProgressReport logger = null, CreateFileOptions options = CreateFileOptions.None)
    {
        const int bufferSize = 0x100000;

        var directoryEntryBuffer = new DirectoryEntry();

        using var sourcePathNormalized = new Path();
        Result rc = InitializeFromString(ref sourcePathNormalized.Ref(), sourcePath);
        if (rc.IsFailure()) return rc;

        using var destPathNormalized = new Path();
        rc = InitializeFromString(ref destPathNormalized.Ref(), destPath);
        if (rc.IsFailure()) return rc;

        byte[] workBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            return CopyDirectoryRecursively(destFs, sourceFs, in destPathNormalized, in sourcePathNormalized,
                ref directoryEntryBuffer, workBuffer, logger, options);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(workBuffer);
            logger?.SetTotal(0);
        }
    }

    public static Result CopyDirectoryRecursively(IFileSystem destinationFileSystem, IFileSystem sourceFileSystem,
        in Path destinationPath, in Path sourcePath, ref DirectoryEntry dirEntry, Span<byte> workBuffer,
        IProgressReport logger = null, CreateFileOptions option = CreateFileOptions.None)
    {
        static Result OnEnterDir(in Path path, in DirectoryEntry entry,
            ref Utility.FsIterationTaskClosure closure)
        {
            Result rc = closure.DestinationPathBuffer.AppendChild(entry.Name);
            if (rc.IsFailure()) return rc;

            return closure.SourceFileSystem.CreateDirectory(in closure.DestinationPathBuffer);
        }

        static Result OnExitDir(in Path path, in DirectoryEntry entry, ref Utility.FsIterationTaskClosure closure)
        {
            return closure.DestinationPathBuffer.RemoveChild();
        }

        Result OnFile(in Path path, in DirectoryEntry entry, ref Utility.FsIterationTaskClosure closure)
        {
            logger?.LogMessage(path.ToString());

            Result result = closure.DestinationPathBuffer.AppendChild(entry.Name);
            if (result.IsFailure()) return result;

            result = CopyFile(closure.DestFileSystem, closure.SourceFileSystem, in closure.DestinationPathBuffer,
                in path, closure.Buffer, logger, option);
            if (result.IsFailure()) return result;

            return closure.DestinationPathBuffer.RemoveChild();
        }

        var taskClosure = new Utility.FsIterationTaskClosure();
        taskClosure.Buffer = workBuffer;
        taskClosure.SourceFileSystem = sourceFileSystem;
        taskClosure.DestFileSystem = destinationFileSystem;

        Result rc = taskClosure.DestinationPathBuffer.Initialize(destinationPath);
        if (rc.IsFailure()) return rc;

        rc = Utility.IterateDirectoryRecursively(sourceFileSystem, in sourcePath, ref dirEntry, OnEnterDir,
            OnExitDir, OnFile, ref taskClosure);

        taskClosure.DestinationPathBuffer.Dispose();
        return rc;
    }

    public static Result CopyFile(IFileSystem destFileSystem, IFileSystem sourceFileSystem, in Path destPath,
        in Path sourcePath, Span<byte> workBuffer, IProgressReport logger = null,
        CreateFileOptions option = CreateFileOptions.None)
    {
        logger?.LogMessage(sourcePath.ToString());

        // Open source file.
        using var sourceFile = new UniqueRef<IFile>();
        Result rc = sourceFileSystem.OpenFile(ref sourceFile.Ref(), sourcePath, OpenMode.Read);
        if (rc.IsFailure()) return rc;

        rc = sourceFile.Get.GetSize(out long fileSize);
        if (rc.IsFailure()) return rc;

        rc = CreateOrOverwriteFile(destFileSystem, in destPath, fileSize, option);
        if (rc.IsFailure()) return rc;

        using var destFile = new UniqueRef<IFile>();
        rc = destFileSystem.OpenFile(ref destFile.Ref(), in destPath, OpenMode.Write);
        if (rc.IsFailure()) return rc;

        // Read/Write file in work buffer sized chunks.
        long remaining = fileSize;
        long offset = 0;

        logger?.SetTotal(fileSize);

        while (remaining > 0)
        {
            rc = sourceFile.Get.Read(out long bytesRead, offset, workBuffer, ReadOption.None);
            if (rc.IsFailure()) return rc;

            rc = destFile.Get.Write(offset, workBuffer.Slice(0, (int)bytesRead), WriteOption.None);
            if (rc.IsFailure()) return rc;

            remaining -= bytesRead;
            offset += bytesRead;

            logger?.ReportAdd(bytesRead);
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

        using var directory = new UniqueRef<IDirectory>();

        using (var pathNormalized = new Path())
        {
            InitializeFromString(ref pathNormalized.Ref(), path).ThrowIfFailure();

            fileSystem.OpenDirectory(ref directory.Ref(), in pathNormalized, OpenDirectoryMode.All)
                .ThrowIfFailure();
        }

        while (true)
        {
            Unsafe.SkipInit(out DirectoryEntry dirEntry);

            directory.Get.Read(out long entriesRead, SpanHelpers.AsSpan(ref dirEntry)).ThrowIfFailure();
            if (entriesRead == 0) break;

            DirectoryEntryEx entry = GetDirectoryEntryEx(ref dirEntry, path);

            if (PathTools.MatchesPattern(searchPattern, entry.Name, ignoreCase))
            {
                yield return entry;
            }

            if (entry.Type != DirectoryEntryType.Directory || !recurse) continue;

            IEnumerable<DirectoryEntryEx> subEntries =
                fileSystem.EnumerateEntries(PathTools.Combine(path, entry.Name), searchPattern,
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
        using var pathNormalized = new Path();
        InitializeFromString(ref pathNormalized.Ref(), path).ThrowIfFailure();

        fs.QueryEntry(Span<byte>.Empty, Span<byte>.Empty, QueryId.SetConcatenationFileAttribute, in pathNormalized);
    }

    public static void CleanDirectoryRecursivelyGeneric(IFileSystem fileSystem, string path)
    {
        IFileSystem fs = fileSystem;

        foreach (DirectoryEntryEx entry in fileSystem.EnumerateEntries(path, "*", SearchOptions.Default))
        {
            string subPath = PathTools.Combine(path, entry.Name);

            using var subPathNormalized = new Path();
            InitializeFromString(ref subPathNormalized.Ref(), subPath).ThrowIfFailure();

            if (entry.Type == DirectoryEntryType.Directory)
            {
                CleanDirectoryRecursivelyGeneric(fileSystem, subPath);
                fs.DeleteDirectory(in subPathNormalized);
            }
            else if (entry.Type == DirectoryEntryType.File)
            {
                fs.DeleteFile(in subPathNormalized);
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
        using var pathNormalized = new Path();
        Result rc = InitializeFromString(ref pathNormalized.Ref(), path);
        if (rc.IsFailure()) return rc;

        return Utility.EnsureDirectory(fs, in pathNormalized);
    }

    public static Result CreateOrOverwriteFile(IFileSystem fileSystem, in Path path, long size,
        CreateFileOptions option = CreateFileOptions.None)
    {
        Result rc = fileSystem.CreateFile(in path, size, option);

        if (rc.IsFailure())
        {
            if (!ResultFs.PathAlreadyExists.Includes(rc))
                return rc;

            rc = fileSystem.DeleteFile(in path);
            if (rc.IsFailure()) return rc;

            rc = fileSystem.CreateFile(in path, size, option);
            if (rc.IsFailure()) return rc;
        }

        return Result.Success;
    }

    private static Result InitializeFromString(ref Path outPath, string path)
    {
        ReadOnlySpan<byte> utf8Path = StringUtils.StringToUtf8(path);

        Result rc = outPath.Initialize(utf8Path);
        if (rc.IsFailure()) return rc;

        var pathFlags = new PathFlags();
        pathFlags.AllowEmptyPath();
        outPath.Normalize(pathFlags);
        if (rc.IsFailure()) return rc;

        return Result.Success;
    }
}

[Flags]
public enum SearchOptions
{
    Default = 0,
    RecurseSubdirectories = 1 << 0,
    CaseInsensitive = 1 << 1
}
