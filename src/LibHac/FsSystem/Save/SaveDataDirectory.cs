using System;
using System.Text;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem.Save
{
    public class SaveDataDirectory : IDirectory
    {
        private SaveDataFileSystemCore ParentFileSystem { get; }

        private OpenDirectoryMode Mode { get; }

        private SaveFindPosition InitialPosition { get; }
        private SaveFindPosition _currentPosition;

        public SaveDataDirectory(SaveDataFileSystemCore fs, SaveFindPosition position, OpenDirectoryMode mode)
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
            SaveFindPosition position = InitialPosition;

            return ReadImpl(out entryCount, ref position, Span<DirectoryEntry>.Empty);
        }

        private Result ReadImpl(out long entriesRead, ref SaveFindPosition position, Span<DirectoryEntry> entryBuffer)
        {
            HierarchicalSaveFileTable tab = ParentFileSystem.FileTable;

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
                        entry.Name[64] = 0;

                        entry.Type = DirectoryEntryType.Directory;
                        entry.Size = 0;
                    }

                    i++;
                }
            }

            if (Mode.HasFlag(OpenDirectoryMode.File))
            {
                while ((entryBuffer.IsEmpty || i < entryBuffer.Length) && tab.FindNextFile(ref position, out SaveFileInfo info, out string name))
                {
                    if (!entryBuffer.IsEmpty)
                    {
                        ref DirectoryEntry entry = ref entryBuffer[i];
                        Span<byte> nameUtf8 = Encoding.UTF8.GetBytes(name);

                        StringUtils.Copy(entry.Name, nameUtf8);
                        entry.Name[64] = 0;

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
