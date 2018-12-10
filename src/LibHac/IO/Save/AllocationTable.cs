using System.IO;

namespace LibHac.IO.Save
{
    public class AllocationTable
    {
        private IStorage BaseStorage { get; }
        private IStorage HeaderStorage { get; }

        public AllocationTableEntry[] Entries { get; }
        public AllocationTableHeader Header { get; }

        public AllocationTable(IStorage storage, IStorage header)
        {
            BaseStorage = storage;
            HeaderStorage = header;
            Header = new AllocationTableHeader(HeaderStorage);

            Stream tableStream = storage.AsStream();

            // The first entry in the table is reserved. Block 0 is at table index 1
            int blockCount = (int)(Header.AllocationTableBlockCount) + 1;

            Entries = new AllocationTableEntry[blockCount];
            tableStream.Position = 0;
            var reader = new BinaryReader(tableStream);

            for (int i = 0; i < blockCount; i++)
            {
                int parent = reader.ReadInt32();
                int child = reader.ReadInt32();

                Entries[i] = new AllocationTableEntry { Next = child, Prev = parent };
            }
        }

        public IStorage GetBaseStorage() => BaseStorage.WithAccess(FileAccess.Read);
        public IStorage GetHeaderStorage() => HeaderStorage.WithAccess(FileAccess.Read);
    }

    public class AllocationTableEntry
    {
        public int Prev { get; set; }
        public int Next { get; set; }

        public bool IsListStart()
        {
            return Prev == int.MinValue;
        }

        public bool IsListEnd()
        {
            return (Next & 0x7FFFFFFF) == 0;
        }

        public bool IsMultiBlockSegment()
        {
            return Next < 0;
        }

        public bool IsSingleBlockSegment()
        {
            return Next >= 0;
        }
    }

    public class AllocationTableHeader
    {
        public long BlockSize { get; }
        public long AllocationTableOffset { get; }
        public long AllocationTableBlockCount { get; }
        public long DataOffset { get; }
        public long DataBlockCount { get; }
        public int DirectoryTableBlock { get; }
        public int FileTableBlock { get; }

        public AllocationTableHeader(IStorage storage)
        {
            var reader = new BinaryReader(storage.AsStream());

            BlockSize = reader.ReadInt64();

            AllocationTableOffset = reader.ReadInt64();
            AllocationTableBlockCount = reader.ReadInt32();
            reader.BaseStream.Position += 4;

            DataOffset = reader.ReadInt64();
            DataBlockCount = reader.ReadInt32();
            reader.BaseStream.Position += 4;

            DirectoryTableBlock = reader.ReadInt32();
            FileTableBlock = reader.ReadInt32();
        }
    }
}
