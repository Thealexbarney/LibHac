using System;
using System.Collections.Generic;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem
{
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

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);

            // Open directories from all layers so they can be merged
            // Only allocate the list for multiple sources if needed
            List<IFileSystem> multipleSources = null;
            IFileSystem singleSource = null;

            foreach (IFileSystem fs in Sources)
            {
                Result rc = fs.GetEntryType(out DirectoryEntryType entryType, path);

                if (rc.IsSuccess())
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
                else if (!ResultFs.PathNotFound.Includes(rc))
                {
                    return rc;
                }
            }

            if (!(multipleSources is null))
            {
                var dir = new MergedDirectory(multipleSources, path, mode);
                Result rc = dir.Initialize();

                if (rc.IsSuccess())
                {
                    directory = dir;
                }

                return rc;
            }

            if (!(singleSource is null))
            {
                Result rc = singleSource.OpenDirectory(out IDirectory dir, path, mode);

                if (rc.IsSuccess())
                {
                    directory = dir;
                }

                return rc;
            }

            return ResultFs.PathNotFound.Log();
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            foreach (IFileSystem fs in Sources)
            {
                Result rc = fs.GetEntryType(out DirectoryEntryType type, path);

                if (rc.IsSuccess())
                {
                    if (type == DirectoryEntryType.File)
                    {
                        return fs.OpenFile(out file, path, mode);
                    }

                    if (type == DirectoryEntryType.Directory)
                    {
                        return ResultFs.PathNotFound.Log();
                    }
                }
                else if (!ResultFs.PathNotFound.Includes(rc))
                {
                    return rc;
                }
            }

            return ResultFs.PathNotFound.Log();
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
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

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, U8Span path)
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
            U8Span path)
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

        protected override Result DoCreateDirectory(U8Span path) => ResultFs.UnsupportedOperation.Log();
        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions options) => ResultFs.UnsupportedOperation.Log();
        protected override Result DoDeleteDirectory(U8Span path) => ResultFs.UnsupportedOperation.Log();
        protected override Result DoDeleteDirectoryRecursively(U8Span path) => ResultFs.UnsupportedOperation.Log();
        protected override Result DoCleanDirectoryRecursively(U8Span path) => ResultFs.UnsupportedOperation.Log();
        protected override Result DoDeleteFile(U8Span path) => ResultFs.UnsupportedOperation.Log();
        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath) => ResultFs.UnsupportedOperation.Log();
        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath) => ResultFs.UnsupportedOperation.Log();

        private class MergedDirectory : IDirectory
        {
            // Needed to open new directories for GetEntryCount
            private List<IFileSystem> SourceFileSystems { get; }
            private List<IDirectory> SourceDirs { get; }
            private U8String Path { get; }
            private OpenDirectoryMode Mode { get; }

            // todo: Efficient way to remove duplicates
            private HashSet<string> Names { get; } = new HashSet<string>();

            public MergedDirectory(List<IFileSystem> sourceFileSystems, U8Span path, OpenDirectoryMode mode)
            {
                SourceFileSystems = sourceFileSystems;
                SourceDirs = new List<IDirectory>(sourceFileSystems.Count);
                Path = path.ToU8String();
                Mode = mode;
            }

            public Result Initialize()
            {
                foreach (IFileSystem fs in SourceFileSystems)
                {
                    Result rc = fs.OpenDirectory(out IDirectory dir, Path, Mode);
                    if (rc.IsFailure()) return rc;

                    SourceDirs.Add(dir);
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
                        Result rs = SourceDirs[i].Read(out subEntriesRead, entryBuffer.Slice(entryIndex, 1));
                        if (rs.IsFailure()) return rs;

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

                // Open new directories for each source because we need to remove duplicate entries
                foreach (IFileSystem fs in SourceFileSystems)
                {
                    Result rc = fs.OpenDirectory(out IDirectory dir, Path, Mode);
                    if (rc.IsFailure()) return rc;

                    long entriesRead;
                    do
                    {
                        rc = dir.Read(out entriesRead, SpanHelpers.AsSpan(ref entry));
                        if (rc.IsFailure()) return rc;

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
}
