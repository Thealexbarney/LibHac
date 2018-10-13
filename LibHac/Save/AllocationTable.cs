using System.IO;

namespace LibHac.Save
{
    public class AllocationTable
    {
        public AllocationTableEntry[] Entries { get; }

        public AllocationTable(Stream tableStream)
        {
            int blockCount = (int)(tableStream.Length / 8);

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
}
