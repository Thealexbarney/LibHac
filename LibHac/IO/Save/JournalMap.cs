using System.IO;

namespace LibHac.IO.Save
{
    public class JournalMap
    {
        public JournalMapHeader Header { get; }
        private JournalMapEntry[] Entries { get; }

        private IStorage HeaderStorage { get; }
        private IStorage MapStorage { get; }
        private IStorage ModifiedPhysicalBlocks { get; }
        private IStorage ModifiedVirtualBlocks { get; }
        private IStorage FreeBlocks { get; }

        public JournalMap(IStorage header, JournalMapParams mapInfo)
        {
            HeaderStorage = header;
            MapStorage = mapInfo.MapStorage;
            ModifiedPhysicalBlocks = mapInfo.PhysicalBlockBitmap;
            ModifiedVirtualBlocks = mapInfo.VirtualBlockBitmap;
            FreeBlocks = mapInfo.FreeBlockBitmap;

            Header = new JournalMapHeader(HeaderStorage);
            Entries = ReadMapEntries(MapStorage, Header.MainDataBlockCount);
        }

        public int GetPhysicalBlock(int virtualBlock)
        {
            return Entries[virtualBlock].PhysicalIndex;
        }

        private static JournalMapEntry[] ReadMapEntries(IStorage mapTable, int count)
        {
            var tableReader = new BinaryReader(mapTable.AsStream());
            var map = new JournalMapEntry[count];

            for (int i = 0; i < count; i++)
            {
                var entry = new JournalMapEntry
                {
                    VirtualIndex = i,
                    PhysicalIndex = tableReader.ReadInt32() & 0x7FFFFFFF
                };

                map[i] = entry;
                tableReader.BaseStream.Position += 4;
            }

            return map;
        }

        public IStorage GetMapStorage() => MapStorage.WithAccess(FileAccess.Read);
        public IStorage GetHeaderStorage() => HeaderStorage.WithAccess(FileAccess.Read);
        public IStorage GetModifiedPhysicalBlocksStorage() => ModifiedPhysicalBlocks.WithAccess(FileAccess.Read);
        public IStorage GetModifiedVirtualBlocksStorage() => ModifiedVirtualBlocks.WithAccess(FileAccess.Read);
        public IStorage GetFreeBlocksStorage() => FreeBlocks.WithAccess(FileAccess.Read);
    }

    public class JournalMapHeader
    {
        public int Version { get; }
        public int MainDataBlockCount { get; }
        public int JournalBlockCount { get; }
        public int FieldC { get; }

        public JournalMapHeader(IStorage storage)
        {
            var reader = new BinaryReader(storage.AsStream());

            Version = reader.ReadInt32();
            MainDataBlockCount = reader.ReadInt32();
            JournalBlockCount = reader.ReadInt32();
            FieldC = reader.ReadInt32();
        }
    }

    public class JournalMapParams
    {
        public IStorage MapStorage { get; set; }
        public IStorage PhysicalBlockBitmap { get; set; }
        public IStorage VirtualBlockBitmap { get; set; }
        public IStorage FreeBlockBitmap { get; set; }
    }
}
