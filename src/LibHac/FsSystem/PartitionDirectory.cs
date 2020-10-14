using System;
using System.IO;
using System.Text;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public class PartitionDirectory : IDirectory
    {
        private PartitionFileSystem ParentFileSystem { get; }
        private OpenDirectoryMode Mode { get; }
        private int CurrentIndex { get; set; }

        public PartitionDirectory(PartitionFileSystem fs, string path, OpenDirectoryMode mode)
        {
            path = PathTools.Normalize(path);

            if (path != "/") throw new DirectoryNotFoundException();

            ParentFileSystem = fs;
            Mode = mode;

            CurrentIndex = 0;
        }

        protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            if (!Mode.HasFlag(OpenDirectoryMode.File))
            {
                entriesRead = 0;
                return Result.Success;
            }

            int entriesRemaining = ParentFileSystem.Files.Length - CurrentIndex;
            int toRead = Math.Min(entriesRemaining, entryBuffer.Length);

            for (int i = 0; i < toRead; i++)
            {
                PartitionFileEntry fileEntry = ParentFileSystem.Files[CurrentIndex];
                ref DirectoryEntry entry = ref entryBuffer[i];

                Span<byte> nameUtf8 = Encoding.UTF8.GetBytes(fileEntry.Name);

                entry.Type = DirectoryEntryType.File;
                entry.Size = fileEntry.Size;

                StringUtils.Copy(entry.Name, nameUtf8);
                entry.Name[PathTools.MaxPathLength] = 0;

                CurrentIndex++;
            }

            entriesRead = toRead;
            return Result.Success;
        }

        protected override Result DoGetEntryCount(out long entryCount)
        {
            int count = 0;

            if (Mode.HasFlag(OpenDirectoryMode.File))
            {
                count += ParentFileSystem.Files.Length;
            }

            entryCount = count;
            return Result.Success;
        }
    }
}