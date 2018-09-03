using System;

namespace LibHac.Savefile
{
    public class AllocationTableIterator
    {
        private AllocationTable Fat { get; }

        public int VirtualBlock { get; private set; }
        public int PhysicalBlock { get; private set; }
        public int CurrentSegmentSize { get; private set; }

        public AllocationTableIterator(AllocationTable table, int initialBlock)
        {
            Fat = table;
            if (!BeginIteration(initialBlock))
            {
                throw new ArgumentException($"Attempted to start FAT iteration from an invalid block. ({initialBlock}");
            }
        }

        public bool BeginIteration(int initialBlock)
        {
            var tableEntry = Fat.Entries[initialBlock + 1];

            if (!tableEntry.IsListStart())
            {
                return false;
            }

            if (tableEntry.IsSingleBlockSegment())
            {
                CurrentSegmentSize = 1;
            }
            else
            {
                var lengthEntry = Fat.Entries[initialBlock + 2];
                CurrentSegmentSize = lengthEntry.Next - initialBlock;
            }

            PhysicalBlock = initialBlock;

            return true;
        }

        public bool MoveNext()
        {
            var currentEntry = Fat.Entries[PhysicalBlock + 1];
            if (currentEntry.IsListEnd()) return false;
            int newBlock = currentEntry.Next & 0x7FFFFFFF;

            var newEntry = Fat.Entries[newBlock];
            VirtualBlock += CurrentSegmentSize;

            if (newEntry.IsSingleBlockSegment())
            {
                CurrentSegmentSize = 1;
            }
            else
            {
                var lengthEntry = Fat.Entries[newBlock + 1];
                CurrentSegmentSize = lengthEntry.Next - (newBlock - 1);
            }

            PhysicalBlock = newBlock - 1;
            return true;
        }

        public bool MovePrevious()
        {
            var currentEntry = Fat.Entries[PhysicalBlock + 1];
            if (currentEntry.IsListStart()) return false;
            int newBlock = currentEntry.Prev & 0x7FFFFFFF;

            var newEntry = Fat.Entries[newBlock];

            if (newEntry.IsSingleBlockSegment())
            {
                CurrentSegmentSize = 1;
            }
            else
            {
                var lengthEntry = Fat.Entries[newBlock + 1];
                CurrentSegmentSize = lengthEntry.Next - (newBlock - 1);
            }

            VirtualBlock -= CurrentSegmentSize;
            PhysicalBlock = newBlock - 1;
            return true;
        }
    }
}
