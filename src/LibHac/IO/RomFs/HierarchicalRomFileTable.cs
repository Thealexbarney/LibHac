using System;
using System.Runtime.CompilerServices;
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

        public HierarchicalRomFileTable() : this(0, 0) { }

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

        public bool TryOpenFile(string path, out RomFileInfo fileInfo)
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

        public bool TryOpenFile(int fileId, out RomFileInfo fileInfo)
        {
            if (FileTable.TryGetValue(fileId, out RomKeyValuePair<FileRomEntry> keyValuePair))
            {
                fileInfo = keyValuePair.Value.Info;
                return true;
            }

            fileInfo = default;
            return false;
        }

        public bool TryOpenDirectory(string path, out FindPosition position)
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

        public bool TryOpenDirectory(int directoryId, out FindPosition position)
        {
            if (DirectoryTable.TryGetValue(directoryId, out RomKeyValuePair<DirectoryRomEntry> keyValuePair))
            {
                position = keyValuePair.Value.Pos;
                return true;
            }

            position = default;
            return false;
        }

        public bool FindNextFile(ref FindPosition position, out RomFileInfo info, out string name)
        {
            if (position.NextFile == -1)
            {
                info = default;
                name = default;
                return false;
            }

            ref FileRomEntry entry = ref FileTable.GetValueReference(position.NextFile, out Span<byte> nameBytes);
            position.NextFile = entry.NextSibling;
            info = entry.Info;

            name = GetUtf8String(nameBytes);

            return true;
        }

        public bool FindNextDirectory(ref FindPosition position, out string name)
        {
            if (position.NextDirectory == -1)
            {
                name = default;
                return false;
            }

            ref DirectoryRomEntry entry = ref DirectoryTable.GetValueReference(position.NextDirectory, out Span<byte> nameBytes);
            position.NextDirectory = entry.NextSibling;
            name = GetUtf8String(nameBytes);

            return true;
        }

        public void CreateFile(string path, ref RomFileInfo fileInfo)
        {
            path = PathTools.Normalize(path);
            ReadOnlySpan<byte> pathBytes = GetUtf8Bytes(path);

            CreateFileRecursiveInternal(pathBytes, ref fileInfo);
        }

        public void CreateDirectory(string path)
        {
            path = PathTools.Normalize(path);

            CreateDirectoryRecursive(GetUtf8Bytes(path));
        }

        public void TrimExcess()
        {
            DirectoryTable.TrimExcess();
            FileTable.TrimExcess();
        }

        private static ReadOnlySpan<byte> GetUtf8Bytes(string value)
        {
            return Encoding.UTF8.GetBytes(value).AsSpan();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetUtf8String(ReadOnlySpan<byte> value)
        {
#if NETFRAMEWORK
            return Encoding.UTF8.GetString(value.ToArray());
#else
            return Encoding.UTF8.GetString(value);
#endif
        }

        private void CreateRootDirectory()
        {
            var key = new RomEntryKey(ReadOnlySpan<byte>.Empty, 0);
            var entry = new DirectoryRomEntry();
            entry.NextSibling = -1;
            entry.Pos.NextDirectory = -1;
            entry.Pos.NextFile = -1;

            DirectoryTable.Add(ref key, ref entry);
        }

        private void CreateDirectoryRecursive(ReadOnlySpan<byte> path)
        {
            var parser = new PathParser(path);
            var key = new RomEntryKey();

            int prevOffset = 0;

            while (parser.TryGetNext(out key.Name))
            {
                int offset = DirectoryTable.GetOffsetFromKey(ref key);
                if (offset < 0)
                {
                    ref DirectoryRomEntry entry = ref DirectoryTable.AddOrGet(ref key, out offset, out _, out _);
                    entry.NextSibling = -1;
                    entry.Pos.NextDirectory = -1;
                    entry.Pos.NextFile = -1;

                    ref DirectoryRomEntry parent = ref DirectoryTable.GetValueReference(prevOffset);

                    if (parent.Pos.NextDirectory == -1)
                    {
                        parent.Pos.NextDirectory = offset;
                    }
                    else
                    {
                        ref DirectoryRomEntry chain = ref DirectoryTable.GetValueReference(parent.Pos.NextDirectory);

                        while (chain.NextSibling != -1)
                        {
                            chain = ref DirectoryTable.GetValueReference(chain.NextSibling);
                        }

                        chain.NextSibling = offset;
                    }
                }

                prevOffset = offset;
                key.Parent = offset;
            }
        }

        private void CreateFileRecursiveInternal(ReadOnlySpan<byte> path, ref RomFileInfo fileInfo)
        {
            var parser = new PathParser(path);
            var key = new RomEntryKey();

            parser.TryGetNext(out key.Name);
            int prevOffset = 0;

            while (!parser.IsFinished())
            {
                int offset = DirectoryTable.GetOffsetFromKey(ref key);
                if (offset < 0)
                {
                    ref DirectoryRomEntry entry = ref DirectoryTable.AddOrGet(ref key, out offset, out _, out _);
                    entry.NextSibling = -1;
                    entry.Pos.NextDirectory = -1;
                    entry.Pos.NextFile = -1;

                    ref DirectoryRomEntry parent = ref DirectoryTable.GetValueReference(prevOffset);

                    if (parent.Pos.NextDirectory == -1)
                    {
                        parent.Pos.NextDirectory = offset;
                    }
                    else
                    {
                        ref DirectoryRomEntry chain = ref DirectoryTable.GetValueReference(parent.Pos.NextDirectory);

                        while (chain.NextSibling != -1)
                        {
                            chain = ref DirectoryTable.GetValueReference(chain.NextSibling);
                        }

                        chain.NextSibling = offset;
                    }
                }

                prevOffset = offset;
                key.Parent = offset;
                parser.TryGetNext(out key.Name);
            }

            {
                ref FileRomEntry entry = ref FileTable.AddOrGet(ref key, out int offset, out _, out _);
                entry.NextSibling = -1;
                entry.Info = fileInfo;

                ref DirectoryRomEntry parent = ref DirectoryTable.GetValueReference(prevOffset);

                if (parent.Pos.NextFile == -1)
                {
                    parent.Pos.NextFile = offset;
                }
                else
                {
                    ref FileRomEntry chain = ref FileTable.GetValueReference(parent.Pos.NextFile);

                    while (chain.NextSibling != -1)
                    {
                        chain = ref FileTable.GetValueReference(chain.NextSibling);
                    }

                    chain.NextSibling = offset;
                }
            }
        }

        private void FindFileRecursive(ReadOnlySpan<byte> path, out RomEntryKey key)
        {
            var parser = new PathParser(path);
            key = default;

            while (parser.TryGetNext(out key.Name) && !parser.IsFinished())
            {
                key.Parent = DirectoryTable.GetOffsetFromKey(ref key);
            }
        }

        private void FindDirectoryRecursive(ReadOnlySpan<byte> path, out RomEntryKey key)
        {
            var parser = new PathParser(path);
            key = default;

            while (parser.TryGetNext(out key.Name) && !parser.IsFinished())
            {
                key.Parent = DirectoryTable.GetOffsetFromKey(ref key);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct DirectoryRomEntry
        {
            public int NextSibling;
            public FindPosition Pos;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct FileRomEntry
        {
            public int NextSibling;
            public RomFileInfo Info;
        }
    }
}
