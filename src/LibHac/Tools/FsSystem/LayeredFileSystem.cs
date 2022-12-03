using System;
using System.Collections.Generic;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.Tools.FsSystem;

public class LayeredFileSystem : IFileSystem
{
    /// <summary>
    /// List of source <see cref="IFileSystem"/>s.
    /// Filesystems at the beginning of the list will take precedence over those later in the list.
    /// </summary>
    private List<IFileSystem> Sources { get; } = new List<IFileSystem>();

    /// <summary>
    /// Creates a new <see cref="LayeredFileSystem"/> from the input <see cref="IFileSystem"/> objects.
    /// </summary>
    /// <param name="lowerFileSystem">The base <see cref="IFileSystem"/>.</param>
    /// <param name="upperFileSystem">The <see cref="IFileSystem"/> to be layered on top of the <paramref name="lowerFileSystem"/>.</param>
    public LayeredFileSystem(IFileSystem lowerFileSystem, IFileSystem upperFileSystem)
    {
        Sources.Add(upperFileSystem);
        Sources.Add(lowerFileSystem);
    }

    /// <summary>
    /// Creates a new <see cref="LayeredFileSystem"/> from the input <see cref="IFileSystem"/> objects.
    /// </summary>
    /// <param name="sourceFileSystems">An <see cref="IList{IFileSystem}"/> containing the <see cref="IFileSystem"/>s
    /// used to create the <see cref="LayeredFileSystem"/>. Filesystems at the beginning of the list will take
    /// precedence over those later in the list.</param>
    public LayeredFileSystem(IList<IFileSystem> sourceFileSystems)
    {
        Sources.AddRange(sourceFileSystems);
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        // Open directories from all layers so they can be merged
        // Only allocate the list for multiple sources if needed
        List<IFileSystem> multipleSources = null;
        IFileSystem singleSource = null;

        foreach (IFileSystem fs in Sources)
        {
            Result res = fs.GetEntryType(out DirectoryEntryType entryType, in path);

            if (res.IsSuccess())
            {
                // There were no directories with this path in higher levels, so the entry is a file
                if (entryType == DirectoryEntryType.File && singleSource is null)
                {
                    return ResultFs.PathNotFound.Log();
                }

                if (entryType == DirectoryEntryType.Directory)
                {
                    if (singleSource is null)
                    {
                        singleSource = fs;
                    }
                    else if (multipleSources is null)
                    {
                        multipleSources = new List<IFileSystem> { singleSource, fs };
                    }
                    else
                    {
                        multipleSources.Add(fs);
                    }
                }
            }
            else if (!ResultFs.PathNotFound.Includes(res))
            {
                return res;
            }
        }

        if (!(multipleSources is null))
        {
            using var dir = new UniqueRef<MergedDirectory>(new MergedDirectory(multipleSources, mode));
            Result res = dir.Get.Initialize(in path);

            if (res.IsSuccess())
            {
                outDirectory.Set(ref dir.Ref);
            }

            return res;
        }

        if (!(singleSource is null))
        {
            using var dir = new UniqueRef<IDirectory>();
            Result res = singleSource.OpenDirectory(ref dir.Ref, in path, mode);

            if (res.IsSuccess())
            {
                outDirectory.Set(ref dir.Ref);
            }

            return res;
        }

        return ResultFs.PathNotFound.Log();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        foreach (IFileSystem fs in Sources)
        {
            Result res = fs.GetEntryType(out DirectoryEntryType type, path);

            if (res.IsSuccess())
            {
                if (type == DirectoryEntryType.File)
                {
                    return fs.OpenFile(ref outFile, path, mode);
                }

                if (type == DirectoryEntryType.Directory)
                {
                    return ResultFs.PathNotFound.Log();
                }
            }
            else if (!ResultFs.PathNotFound.Includes(res))
            {
                return res;
            }
        }

        return ResultFs.PathNotFound.Log();
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out entryType);

        foreach (IFileSystem fs in Sources)
        {
            Result getEntryResult = fs.GetEntryType(out DirectoryEntryType type, path);

            if (getEntryResult.IsSuccess())
            {
                entryType = type;
                return Result.Success;
            }
        }

        return ResultFs.PathNotFound.Log();
    }

    protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out timeStamp);

        foreach (IFileSystem fs in Sources)
        {
            Result getEntryResult = fs.GetEntryType(out _, path);

            if (getEntryResult.IsSuccess())
            {
                return fs.GetFileTimeStampRaw(out timeStamp, path);
            }
        }

        return ResultFs.PathNotFound.Log();
    }

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
        in Path path)
    {
        foreach (IFileSystem fs in Sources)
        {
            Result getEntryResult = fs.GetEntryType(out _, path);

            if (getEntryResult.IsSuccess())
            {
                return fs.QueryEntry(outBuffer, inBuffer, queryId, path);
            }
        }

        return ResultFs.PathNotFound.Log();
    }

    protected override Result DoCommit()
    {
        return Result.Success;
    }

    protected override Result DoCreateDirectory(in Path path) => ResultFs.UnsupportedOperation.Log();
    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option) => ResultFs.UnsupportedOperation.Log();
    protected override Result DoDeleteDirectory(in Path path) => ResultFs.UnsupportedOperation.Log();
    protected override Result DoDeleteDirectoryRecursively(in Path path) => ResultFs.UnsupportedOperation.Log();
    protected override Result DoCleanDirectoryRecursively(in Path path) => ResultFs.UnsupportedOperation.Log();
    protected override Result DoDeleteFile(in Path path) => ResultFs.UnsupportedOperation.Log();
    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath) => ResultFs.UnsupportedOperation.Log();
    protected override Result DoRenameFile(in Path currentPath, in Path newPath) => ResultFs.UnsupportedOperation.Log();

    private class MergedDirectory : IDirectory
    {
        // Needed to open new directories for GetEntryCount
        private List<IFileSystem> SourceFileSystems { get; }
        private List<IDirectory> SourceDirs { get; }
        private Path.Stored _path;
        private OpenDirectoryMode Mode { get; }

        // todo: Efficient way to remove duplicates
        private HashSet<string> Names { get; } = new HashSet<string>();

        public MergedDirectory(List<IFileSystem> sourceFileSystems, OpenDirectoryMode mode)
        {
            SourceFileSystems = sourceFileSystems;
            SourceDirs = new List<IDirectory>(sourceFileSystems.Count);
            Mode = mode;
        }

        public Result Initialize(in Path path)
        {
            Result res = _path.Initialize(in path);
            if (res.IsFailure()) return res.Miss();

            using var dir = new UniqueRef<IDirectory>();

            foreach (IFileSystem fs in SourceFileSystems)
            {
                res = fs.OpenDirectory(ref dir.Ref, in path, Mode);
                if (res.IsFailure()) return res.Miss();

                SourceDirs.Add(dir.Release());
            }

            return Result.Success;
        }

        protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            entriesRead = 0;
            int entryIndex = 0;

            for (int i = 0; i < SourceDirs.Count && entryIndex < entryBuffer.Length; i++)
            {
                long subEntriesRead;

                do
                {
                    Result res = SourceDirs[i].Read(out subEntriesRead, entryBuffer.Slice(entryIndex, 1));
                    if (res.IsFailure()) return res;

                    if (subEntriesRead == 1 && Names.Add(StringUtils.Utf8ZToString(entryBuffer[entryIndex].Name)))
                    {
                        entryIndex++;
                    }
                } while (subEntriesRead != 0 && entryIndex < entryBuffer.Length);
            }

            entriesRead = entryIndex;
            return Result.Success;
        }

        protected override Result DoGetEntryCount(out long entryCount)
        {
            entryCount = 0;
            long totalEntryCount = 0;
            var entry = new DirectoryEntry();

            // todo: Efficient way to remove duplicates
            var names = new HashSet<string>();

            using Path path = _path.DangerousGetPath();
            using var dir = new UniqueRef<IDirectory>();

            // Open new directories for each source because we need to remove duplicate entries
            foreach (IFileSystem fs in SourceFileSystems)
            {
                Result res = fs.OpenDirectory(ref dir.Ref, in path, Mode);
                if (res.IsFailure()) return res.Miss();

                long entriesRead;
                do
                {
                    res = dir.Get.Read(out entriesRead, SpanHelpers.AsSpan(ref entry));
                    if (res.IsFailure()) return res.Miss();

                    if (entriesRead == 1 && names.Add(StringUtils.Utf8ZToString(entry.Name)))
                    {
                        totalEntryCount++;
                    }
                } while (entriesRead != 0);
            }

            entryCount = totalEntryCount;
            return Result.Success;
        }
    }
}