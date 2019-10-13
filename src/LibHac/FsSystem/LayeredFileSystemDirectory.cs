using System;
using System.Collections.Generic;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class LayeredFileSystemDirectory : IDirectory
    {
        private List<IDirectory> Sources { get; }

        public LayeredFileSystemDirectory(List<IDirectory> sources)
        {
            Sources = sources;
        }

        // Todo: Don't return duplicate entries
        public Result Read(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            entriesRead = 0;
            int entryIndex = 0;

            for (int i = 0; i < Sources.Count && entryIndex < entryBuffer.Length; i++)
            {
                Result rs = Sources[i].Read(out long subEntriesRead, entryBuffer.Slice(entryIndex));
                if (rs.IsFailure()) return rs;

                entryIndex += (int)subEntriesRead;
            }

            entriesRead = entryIndex;
            return Result.Success;
        }

        // Todo: Don't count duplicate entries
        public Result GetEntryCount(out long entryCount)
        {
            entryCount = 0;
            long totalEntryCount = 0;

            foreach (IDirectory dir in Sources)
            {
                Result rc = dir.GetEntryCount(out long subEntryCount);
                if (rc.IsFailure()) return rc;

                totalEntryCount += subEntryCount;
            }

            entryCount = totalEntryCount;
            return Result.Success;
        }
    }
}
