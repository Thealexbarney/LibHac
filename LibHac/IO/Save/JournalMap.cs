using System.ComponentModel;
using System.IO;

namespace LibHac.IO.Save
{
    public class JournalMap
    {
        public JournalMapHeader Header { get; }
        private JournalMapEntry[] Entries { get; }

        private Storage HeaderStorage { get; }
        private Storage MapStorage { get; }
        private Storage ModifiedPhysicalBlocks { get; }
        private Storage ModifiedVirtualBlocks { get; }
        private Storage FreeBlocks { get; }

        public JournalMap(Storage header, JournalMapParams mapInfo)
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

        private static JournalMapEntry[] ReadMapEntries(Storage mapTable, int count)
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

        [EditorBrowsable(EditorBrowsableState.Never)]
        public Storage GetMapStorage() => MapStorage.Clone(true, FileAccess.Read);
        public Storage GetHeaderStorage() => HeaderStorage.Clone(true, FileAccess.Read);
        public Storage GetModifiedPhysicalBlocksStorage() => ModifiedPhysicalBlocks.Clone(true, FileAccess.Read);
        public Storage GetModifiedVirtualBlocksStorage() => ModifiedVirtualBlocks.Clone(true, FileAccess.Read);
        public Storage GetFreeBlocksStorage() => FreeBlocks.Clone(true, FileAccess.Read);
    }

    public class JournalMapHeader
    {
        public int Version { get; }
        public int MainDataBlockCount { get; }
        public int JournalBlockCount { get; }
        public int FieldC { get; }

        public JournalMapHeader(Storage storage)
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
        public Storage MapStorage { get; set; }
        public Storage PhysicalBlockBitmap { get; set; }
        public Storage VirtualBlockBitmap { get; set; }
        public Storage FreeBlockBitmap { get; set; }
    }
}
