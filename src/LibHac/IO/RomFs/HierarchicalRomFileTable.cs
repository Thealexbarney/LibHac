using System;
using System.Text;

namespace LibHac.IO.RomFs
{
    public class HierarchicalRomFileTable
    {
        private IStorage DirHashTableStorage { get; }
        private IStorage DirEntryTableStorage { get; }
        private IStorage FileHashTableStorage { get; }
        private IStorage FileEntryTableStorage { get; }

        private RomFsDictionary<FileRomEntry> FileTable { get; }
        private RomFsDictionary<DirectoryRomEntry> DirectoryTable { get; }

        public HierarchicalRomFileTable(IStorage dirHashTable, IStorage dirEntryTable, IStorage fileHashTable,
            IStorage fileEntryTable)
        {
            DirHashTableStorage = dirHashTable;
            DirEntryTableStorage = dirEntryTable;
            FileHashTableStorage = fileHashTable;
            FileEntryTableStorage = fileEntryTable;

            FileTable = new RomFsDictionary<FileRomEntry>(FileHashTableStorage, FileEntryTableStorage);
            DirectoryTable = new RomFsDictionary<DirectoryRomEntry>(DirHashTableStorage, DirEntryTableStorage);
        }

        public bool OpenFile(string path, out RomFileInfo fileInfo)
        {
            FindFileRecursive(GetUtf8Bytes(path), out RomEntryKey key);

            if (FileTable.TryGetValue(ref key, out RomKeyValuePair<FileRomEntry> keyValuePair))
            {
                fileInfo = keyValuePair.Value.Info;
                return true;
            }

            fileInfo = default;
            return false;
        }

        public bool OpenFile(int offset, out RomFileInfo fileInfo)
        {
            if (FileTable.TryGetValue(offset, out RomKeyValuePair<FileRomEntry> keyValuePair))
            {
                fileInfo = keyValuePair.Value.Info;
                return true;
            }

            fileInfo = default;
            return false;
        }

        public bool OpenDirectory(string path, out FindPosition position)
        {
            FindDirectoryRecursive(GetUtf8Bytes(path), out RomEntryKey key);

            if (DirectoryTable.TryGetValue(ref key, out RomKeyValuePair<DirectoryRomEntry> keyValuePair))
            {
                position = keyValuePair.Value.Pos;
                return true;
            }

            position = default;
            return false;
        }

        public bool OpenDirectory(int offset, out FindPosition position)
        {
            if (DirectoryTable.TryGetValue(offset, out RomKeyValuePair<DirectoryRomEntry> keyValuePair))
            {
                position = keyValuePair.Value.Pos;
                return true;
            }

            position = default;
            return false;
        }

        private static ReadOnlySpan<byte> GetUtf8Bytes(string value)
        {
            return Encoding.UTF8.GetBytes(value).AsSpan();
        }

        public bool FindNextFile(ref FindPosition position, out RomFileInfo info, out string name)
        {
            if (FileTable.TryGetValue(position.NextFile, out RomKeyValuePair<FileRomEntry> keyValuePair))
            {
                position.NextFile = keyValuePair.Value.NextSibling;
                info = keyValuePair.Value.Info;
                name = Encoding.UTF8.GetString(keyValuePair.Key.Name.ToArray());
                return true;
            }

            info = default;
            name = default;
            return false;
        }

        public bool FindNextDirectory(ref FindPosition position, out string name)
        {
            if (DirectoryTable.TryGetValue(position.NextDirectory, out RomKeyValuePair<DirectoryRomEntry> keyValuePair))
            {
                position.NextDirectory = keyValuePair.Value.NextSibling;
                name = Encoding.UTF8.GetString(keyValuePair.Key.Name.ToArray());
                return true;
            }

            name = default;
            return false;
        }

        private void FindFileRecursive(ReadOnlySpan<byte> path, out RomEntryKey key)
        {
            var parser = new PathParser(path);
            FindParentDirectoryRecursive(ref parser, out RomKeyValuePair<DirectoryRomEntry> keyValuePair);

            key = keyValuePair.Key;
        }

        private void FindDirectoryRecursive(ReadOnlySpan<byte> path, out RomEntryKey key)
        {
            var parser = new PathParser(path);
            FindParentDirectoryRecursive(ref parser, out RomKeyValuePair<DirectoryRomEntry> keyValuePair);

            ReadOnlySpan<byte> name = parser.GetCurrent();
            int parentOffset = name.Length == 0 ? 0 : keyValuePair.Offset;

            key = new RomEntryKey(name, parentOffset);
        }

        private void FindParentDirectoryRecursive(ref PathParser parser, out RomKeyValuePair<DirectoryRomEntry> keyValuePair)
        {
            keyValuePair = default;
            RomEntryKey key = default;

            while (parser.TryGetNext(out key.Name) && !parser.IsFinished())
            {
                DirectoryTable.TryGetValue(ref key, out keyValuePair);
                key.Parent = keyValuePair.Offset;
            }
        }
    }
}
