using System;
using LibHac.Common;

#if CROSS_PLATFORM
using System.Runtime.InteropServices;
#endif

namespace LibHac.Fs
{
    public class ConcatenationDirectory : IDirectory
    {
        private string Path { get; }
        private OpenDirectoryMode Mode { get; }

        private ConcatenationFileSystem ParentFileSystem { get; }
        private IFileSystem BaseFileSystem { get; }
        private IDirectory ParentDirectory { get; }

        public ConcatenationDirectory(ConcatenationFileSystem fs, IFileSystem baseFs, IDirectory parentDirectory, OpenDirectoryMode mode, string path)
        {
            ParentFileSystem = fs;
            BaseFileSystem = baseFs;
            ParentDirectory = parentDirectory;
            Mode = mode;
            Path = path;
        }

        public Result Read(out long entriesRead, Span<DirectoryEntry> entryBuffer)
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
                        string entryName = Util.GetUtf8StringNullTerminated(entry.Name);
                        string entryFullPath = PathTools.Combine(Path, entryName);

                        entry.Size = ParentFileSystem.GetConcatenationFileSize(entryFullPath);
                    }
                }

                entry.Attributes = NxFileAttributes.None;

                entryBuffer[i] = entry;
            }

            entriesRead = i;
            return Result.Success;
        }

        public Result GetEntryCount(out long entryCount)
        {
            entryCount = 0;
            long count = 0;

            Result rc = BaseFileSystem.OpenDirectory(out IDirectory _, Path,
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
#if CROSS_PLATFORM
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ConcatenationFileSystem.HasConcatenationFileAttribute(entry.Attributes);
            }
            else
            {
                string name = Util.GetUtf8StringNullTerminated(entry.Name);
                string fullPath = PathTools.Combine(Path, name);

                return ParentFileSystem.IsConcatenationFile(fullPath);
            }
#else
            return ConcatenationFileSystem.HasConcatenationFileAttribute(entry.Attributes);
#endif
        }
    }
}
