using System.IO;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem.Save
{
    public class JournalMap
    {
        private int MapEntryLength = 8;
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

        public IStorage GetMapStorage() => MapStorage;
        public IStorage GetHeaderStorage() => HeaderStorage;
        public IStorage GetModifiedPhysicalBlocksStorage() => ModifiedPhysicalBlocks;
        public IStorage GetModifiedVirtualBlocksStorage() => ModifiedVirtualBlocks;
        public IStorage GetFreeBlocksStorage() => FreeBlocks;

        public void FsTrim()
        {
            int virtualBlockCount = Header.MainDataBlockCount;
            int physicalBlockCount = virtualBlockCount + Header.JournalBlockCount;

            int blockMapLength = virtualBlockCount * MapEntryLength;
            int physicalBitmapLength = Alignment.AlignUp(physicalBlockCount, 32) / 8;
            int virtualBitmapLength = Alignment.AlignUp(virtualBlockCount, 32) / 8;

            MapStorage.Slice(blockMapLength).Fill(SaveDataFileSystem.TrimFillValue);
            FreeBlocks.Slice(physicalBitmapLength).Fill(SaveDataFileSystem.TrimFillValue);
            ModifiedPhysicalBlocks.Slice(physicalBitmapLength).Fill(SaveDataFileSystem.TrimFillValue);
            ModifiedVirtualBlocks.Slice(virtualBitmapLength).Fill(SaveDataFileSystem.TrimFillValue);
        }
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
