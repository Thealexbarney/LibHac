using System;
using System.Runtime.InteropServices;
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

        public HierarchicalRomFileTable(int directoryCapacity, int fileCapacity)
        {
            FileTable = new RomFsDictionary<FileRomEntry>(fileCapacity);
            DirectoryTable = new RomFsDictionary<DirectoryRomEntry>(directoryCapacity);

            CreateRootDirectory();
        }

        public byte[] GetDirectoryBuckets()
        {
            return MemoryMarshal.Cast<int, byte>(DirectoryTable.GetBucketData()).ToArray();
        }

        public byte[] GetDirectoryEntries()
        {
            return DirectoryTable.GetEntryData().ToArray();
        }

        public byte[] GetFileBuckets()
        {
            return MemoryMarshal.Cast<int, byte>(FileTable.GetBucketData()).ToArray();
        }

        public byte[] GetFileEntries()
        {
            return FileTable.GetEntryData().ToArray();
        }

        public bool OpenFile(string path, out RomFileInfo fileInfo)
        {
            FindFileRecursive(GetUtf8Bytes(path), out RomEntryKey key, out _);

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
            FindDirectoryRecursive(GetUtf8Bytes(path), out RomEntryKey key, out _);

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

        public void CreateRootDirectory()
        {
            var key = new RomEntryKey(ReadOnlySpan<byte>.Empty, 0);
            var entry = new DirectoryRomEntry();
            entry.NextSibling = -1;
            entry.Pos.NextDirectory = -1;
            entry.Pos.NextFile = -1;

            DirectoryTable.Insert(ref key, ref entry);
        }

        public void CreateFile(string path, ref RomFileInfo fileInfo)
        {
            path = PathTools.Normalize(path);
            ReadOnlySpan<byte> pathBytes = GetUtf8Bytes(path);

            ReadOnlySpan<byte> parentPath = PathTools.GetParentDirectory(pathBytes);
            CreateDirectoryRecursiveInternal(parentPath);

            FindFileRecursive(pathBytes, out RomEntryKey key, out RomKeyValuePair<DirectoryRomEntry> parentEntry);

            if (EntryExists(ref key))
            {
                throw new ArgumentException("Path already exists.");
            }

            var entry = new FileRomEntry();
            entry.NextSibling = -1;
            entry.Info = fileInfo;

            int offset = FileTable.Insert(ref key, ref entry);

            if (parentEntry.Value.Pos.NextFile == -1)
            {
                parentEntry.Value.Pos.NextFile = offset;

                DirectoryTable.TrySetValue(ref parentEntry.Key, ref parentEntry.Value);
                return;
            }

            int nextOffset = parentEntry.Value.Pos.NextFile;

            while (FileTable.TryGetValue(nextOffset, out RomKeyValuePair<FileRomEntry> chainEntry))
            {
                if (chainEntry.Value.NextSibling == -1)
                {
                    chainEntry.Value.NextSibling = offset;
                    FileTable.TrySetValue(ref chainEntry.Key, ref chainEntry.Value);

                    return;
                }

                nextOffset = chainEntry.Value.NextSibling;
            }
        }

        public void CreateDirectoryRecursive(string path)
        {
            path = PathTools.Normalize(path);

            CreateDirectoryRecursiveInternal(GetUtf8Bytes(path));
        }

        private void CreateDirectoryRecursiveInternal(ReadOnlySpan<byte> path)
        {
            for (int i = 1; i < path.Length; i++)
            {
                if (path[i] == '/')
                {
                    ReadOnlySpan<byte> subPath = path.Slice(0, i);
                    CreateDirectoryInternal(subPath);
                }
            }

            CreateDirectoryInternal(path);
        }

        public void CreateDirectory(string path)
        {
            path = PathTools.Normalize(path);

            CreateDirectoryInternal(GetUtf8Bytes(path));
        }

        private void CreateDirectoryInternal(ReadOnlySpan<byte> path)
        {
            FindDirectoryRecursive(path, out RomEntryKey key, out RomKeyValuePair<DirectoryRomEntry> parentEntry);

            if (EntryExists(ref key))
            {
                return;
                // throw new ArgumentException("Path already exists.");
            }

            var entry = new DirectoryRomEntry();
            entry.NextSibling = -1;
            entry.Pos.NextDirectory = -1;
            entry.Pos.NextFile = -1;

            int offset = DirectoryTable.Insert(ref key, ref entry);

            if (parentEntry.Value.Pos.NextDirectory == -1)
            {
                parentEntry.Value.Pos.NextDirectory = offset;

                DirectoryTable.TrySetValue(ref parentEntry.Key, ref parentEntry.Value);
                return;
            }

            int nextOffset = parentEntry.Value.Pos.NextDirectory;

            while (nextOffset != -1)
            {
                DirectoryTable.TryGetValue(nextOffset, out RomKeyValuePair<DirectoryRomEntry> chainEntry);
                if (chainEntry.Value.NextSibling == -1)
                {
                    chainEntry.Value.NextSibling = offset;
                    DirectoryTable.TrySetValue(ref chainEntry.Key, ref chainEntry.Value);

                    return;
                }

                nextOffset = chainEntry.Value.NextSibling;
            }
        }

        private void FindFileRecursive(ReadOnlySpan<byte> path, out RomEntryKey key, out RomKeyValuePair<DirectoryRomEntry> parentEntry)
        {
            var parser = new PathParser(path);
            FindParentDirectoryRecursive(ref parser, out parentEntry);

            key = new RomEntryKey(parser.GetCurrent(), parentEntry.Offset);
        }

        private void FindDirectoryRecursive(ReadOnlySpan<byte> path, out RomEntryKey key, out RomKeyValuePair<DirectoryRomEntry> parentEntry)
        {
            var parser = new PathParser(path);
            FindParentDirectoryRecursive(ref parser, out parentEntry);

            ReadOnlySpan<byte> name = parser.GetCurrent();
            int parentOffset = name.Length == 0 ? 0 : parentEntry.Offset;

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

            // The above loop won't run for top-level directories, so 
            // manually return the root directory for them
            if (key.Parent == 0)
            {
                DirectoryTable.TryGetValue(0, out keyValuePair);
            }
        }

        private bool EntryExists(ref RomEntryKey key)
        {
            return DirectoryTable.ContainsKey(ref key) ||
                   FileTable.ContainsKey(ref key);
        }
    }
}
