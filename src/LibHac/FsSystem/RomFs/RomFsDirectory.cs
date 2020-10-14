using System;
using System.Text;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem.RomFs
{
    public class RomFsDirectory : IDirectory
    {
        private RomFsFileSystem ParentFileSystem { get; }

        private OpenDirectoryMode Mode { get; }

        private FindPosition InitialPosition { get; }
        private FindPosition _currentPosition;

        public RomFsDirectory(RomFsFileSystem fs, FindPosition position, OpenDirectoryMode mode)
        {
            ParentFileSystem = fs;
            InitialPosition = position;
            _currentPosition = position;
            Mode = mode;
        }

        protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            return ReadImpl(out entriesRead, ref _currentPosition, entryBuffer);
        }

        protected override Result DoGetEntryCount(out long entryCount)
        {
            FindPosition position = InitialPosition;

            return ReadImpl(out entryCount, ref position, Span<DirectoryEntry>.Empty);
        }

        private Result ReadImpl(out long entriesRead, ref FindPosition position, Span<DirectoryEntry> entryBuffer)
        {
            HierarchicalRomFileTable<RomFileInfo> tab = ParentFileSystem.FileTable;

            int i = 0;

            if (Mode.HasFlag(OpenDirectoryMode.Directory))
            {
                while ((entryBuffer.IsEmpty || i < entryBuffer.Length) && tab.FindNextDirectory(ref position, out string name))
                {
                    if (!entryBuffer.IsEmpty)
                    {
                        ref DirectoryEntry entry = ref entryBuffer[i];
                        Span<byte> nameUtf8 = Encoding.UTF8.GetBytes(name);

                        StringUtils.Copy(entry.Name, nameUtf8);
                        entry.Name[PathTools.MaxPathLength] = 0;

                        entry.Type = DirectoryEntryType.Directory;
                        entry.Size = 0;
                    }

                    i++;
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.File))
            {
                while ((entryBuffer.IsEmpty || i < entryBuffer.Length) && tab.FindNextFile(ref position, out RomFileInfo info, out string name))
                {
                    if (!entryBuffer.IsEmpty)
                    {
                        ref DirectoryEntry entry = ref entryBuffer[i];
                        Span<byte> nameUtf8 = Encoding.UTF8.GetBytes(name);

                        StringUtils.Copy(entry.Name, nameUtf8);
                        entry.Name[PathTools.MaxPathLength] = 0;

                        entry.Type = DirectoryEntryType.File;
                        entry.Size = info.Length;
                    }

                    i++;
                }
            }

            entriesRead = i;

            return Result.Success;
        }
    }
}
