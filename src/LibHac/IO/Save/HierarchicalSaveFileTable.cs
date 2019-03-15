using System;
using System.Runtime.InteropServices;

namespace LibHac.IO.Save
{
    public class HierarchicalSaveFileTable
    {
        private SaveFsList<FileSaveEntry> FileTable { get; }
        private SaveFsList<DirectorySaveEntry> DirectoryTable { get; }

        public HierarchicalSaveFileTable(IStorage dirTable, IStorage fileTable)
        {
            FileTable = new SaveFsList<FileSaveEntry>(fileTable);
            DirectoryTable = new SaveFsList<DirectorySaveEntry>(dirTable);
        }

        public bool TryOpenFile(string path, out SaveFileInfo fileInfo)
        {
            FindPathRecursive(Util.GetUtf8Bytes(path), out SaveEntryKey key);

            if (FileTable.TryGetValue(ref key, out FileSaveEntry value))
            {
                fileInfo = value.Info;
                return true;
            }

            fileInfo = default;
            return false;
        }

        public bool FindNextFile(ref SaveFindPosition position, out SaveFileInfo info, out string name)
        {
            if (position.NextFile == 0)
            {
                info = default;
                name = default;
                return false;
            }

            Span<byte> nameBytes = stackalloc byte[FileTable.MaxNameLength];

            bool success = FileTable.TryGetValue((int)position.NextFile, out FileSaveEntry entry, ref nameBytes);

            // todo error message
            if (!success)
            {
                info = default;
                name = default;
                return false;
            }

            position.NextFile = entry.NextSibling;
            info = entry.Info;

            name = Util.GetUtf8StringNullTerminated(nameBytes);

            return true;
        }

        public bool FindNextDirectory(ref SaveFindPosition position, out string name)
        {
            if (position.NextDirectory == 0)
            {
                name = default;
                return false;
            }

            Span<byte> nameBytes = stackalloc byte[FileTable.MaxNameLength];

            bool success = DirectoryTable.TryGetValue(position.NextDirectory, out DirectorySaveEntry entry, ref nameBytes);

            // todo error message
            if (!success)
            {
                name = default;
                return false;
            }

            position.NextDirectory = entry.NextSibling;

            name = Util.GetUtf8StringNullTerminated(nameBytes);

            return true;
        }

        public bool TryOpenDirectory(string path, out SaveFindPosition position)
        {
            FindPathRecursive(Util.GetUtf8Bytes(path), out SaveEntryKey key);

            if (DirectoryTable.TryGetValue(ref key, out DirectorySaveEntry value))
            {
                position = value.Pos;
                return true;
            }

            position = default;
            return false;
        }

        private void FindPathRecursive(ReadOnlySpan<byte> path, out SaveEntryKey key)
        {
            var parser = new PathParser(path);
            key = default;

            do
            {
                key.Parent = DirectoryTable.GetOffsetFromKey(ref key);
            } while (parser.TryGetNext(out key.Name) && !parser.IsFinished());
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DirectorySaveEntry
        {
            public int NextSibling;
            public SaveFindPosition Pos;
            public long Field10;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FileSaveEntry
        {
            public int NextSibling;
            public SaveFileInfo Info;
            public long Field10;
        }
    }
}
