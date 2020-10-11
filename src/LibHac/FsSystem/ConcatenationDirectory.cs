using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public class ConcatenationDirectory : IDirectory
    {
        private OpenDirectoryMode Mode { get; }
        private IDirectory ParentDirectory { get; }
        private IFileSystem BaseFileSystem { get; }
        private ConcatenationFileSystem ParentFileSystem { get; }

        private FsPath _path;

        public ConcatenationDirectory(ConcatenationFileSystem fs, IFileSystem baseFs, IDirectory parentDirectory, OpenDirectoryMode mode, U8Span path)
        {
            ParentFileSystem = fs;
            BaseFileSystem = baseFs;
            ParentDirectory = parentDirectory;
            Mode = mode;

            StringUtils.Copy(_path.Str, path);
            _path.Str[PathTools.MaxPathLength] = StringTraits.NullTerminator;

            // Ensure the path ends with a separator
            int pathLength = StringUtils.GetLength(path, PathTools.MaxPathLength + 1);

            if (pathLength != 0 && _path.Str[pathLength - 1] == StringTraits.DirectorySeparator)
                return;

            if (pathLength >= PathTools.MaxPathLength)
                throw new HorizonResultException(ResultFs.TooLongPath.Value, "abort");

            _path.Str[pathLength] = StringTraits.DirectorySeparator;
            _path.Str[pathLength + 1] = StringTraits.NullTerminator;
            _path.Str[PathTools.MaxPathLength] = StringTraits.NullTerminator;
        }

        protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            entriesRead = 0;
            var entry = new DirectoryEntry();
            Span<DirectoryEntry> entrySpan = SpanHelpers.AsSpan(ref entry);

            int i;
            for (i = 0; i < entryBuffer.Length; i++)
            {
                Result rc = ParentDirectory.Read(out long baseEntriesRead, entrySpan);
                if (rc.IsFailure()) return rc;

                if (baseEntriesRead == 0) break;

                // Check if the current open mode says we should return the entry
                bool isConcatFile = IsConcatenationFile(entry);
                if (!CanReturnEntry(entry, isConcatFile)) continue;

                if (isConcatFile)
                {
                    entry.Type = DirectoryEntryType.File;

                    if (!Mode.HasFlag(OpenDirectoryMode.NoFileSize))
                    {
                        string entryName = StringUtils.NullTerminatedUtf8ToString(entry.Name);
                        string entryFullPath = PathTools.Combine(_path.ToString(), entryName);

                        rc = ParentFileSystem.GetConcatenationFileSize(out long fileSize, entryFullPath.ToU8Span());
                        if (rc.IsFailure()) return rc;

                        entry.Size = fileSize;
                    }
                }

                entry.Attributes = NxFileAttributes.None;

                entryBuffer[i] = entry;
            }

            entriesRead = i;
            return Result.Success;
        }

        protected override Result DoGetEntryCount(out long entryCount)
        {
            entryCount = 0;
            long count = 0;

            Result rc = BaseFileSystem.OpenDirectory(out IDirectory _, _path,
                OpenDirectoryMode.All | OpenDirectoryMode.NoFileSize);
            if (rc.IsFailure()) return rc;

            var entry = new DirectoryEntry();
            Span<DirectoryEntry> entrySpan = SpanHelpers.AsSpan(ref entry);

            while (true)
            {
                rc = ParentDirectory.Read(out long baseEntriesRead, entrySpan);
                if (rc.IsFailure()) return rc;

                if (baseEntriesRead == 0) break;

                if (CanReturnEntry(entry, IsConcatenationFile(entry))) count++;
            }

            entryCount = count;
            return Result.Success;
        }

        private bool CanReturnEntry(DirectoryEntry entry, bool isConcatFile)
        {
            return Mode.HasFlag(OpenDirectoryMode.File) && (entry.Type == DirectoryEntryType.File || isConcatFile) ||
                   Mode.HasFlag(OpenDirectoryMode.Directory) && entry.Type == DirectoryEntryType.Directory && !isConcatFile;
        }

        private bool IsConcatenationFile(DirectoryEntry entry)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ConcatenationFileSystem.HasConcatenationFileAttribute(entry.Attributes);
            }
            else
            {
                string name = StringUtils.NullTerminatedUtf8ToString(entry.Name);
                var fullPath = PathTools.Combine(_path.ToString(), name).ToU8Span();

                return ParentFileSystem.IsConcatenationFile(fullPath);
            }
        }
    }
}
