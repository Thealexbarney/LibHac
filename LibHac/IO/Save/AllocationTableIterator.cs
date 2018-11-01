using System;

namespace LibHac.IO.Save
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
            AllocationTableEntry tableEntry = Fat.Entries[initialBlock + 1];

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
                AllocationTableEntry lengthEntry = Fat.Entries[initialBlock + 2];
                CurrentSegmentSize = lengthEntry.Next - initialBlock;
            }

            PhysicalBlock = initialBlock;

            return true;
        }

        public bool MoveNext()
        {
            AllocationTableEntry currentEntry = Fat.Entries[PhysicalBlock + 1];
            if (currentEntry.IsListEnd()) return false;
            int newBlock = currentEntry.Next & 0x7FFFFFFF;

            AllocationTableEntry newEntry = Fat.Entries[newBlock];
            VirtualBlock += CurrentSegmentSize;

            if (newEntry.IsSingleBlockSegment())
            {
                CurrentSegmentSize = 1;
            }
            else
            {
                AllocationTableEntry lengthEntry = Fat.Entries[newBlock + 1];
                CurrentSegmentSize = lengthEntry.Next - (newBlock - 1);
            }

            PhysicalBlock = newBlock - 1;
            return true;
        }

        public bool MovePrevious()
        {
            AllocationTableEntry currentEntry = Fat.Entries[PhysicalBlock + 1];
            if (currentEntry.IsListStart()) return false;
            int newBlock = currentEntry.Prev & 0x7FFFFFFF;

            AllocationTableEntry newEntry = Fat.Entries[newBlock];

            if (newEntry.IsSingleBlockSegment())
            {
                CurrentSegmentSize = 1;
            }
            else
            {
                AllocationTableEntry lengthEntry = Fat.Entries[newBlock + 1];
                CurrentSegmentSize = lengthEntry.Next - (newBlock - 1);
            }

            VirtualBlock -= CurrentSegmentSize;
            PhysicalBlock = newBlock - 1;
            return true;
        }

        public bool Seek(int block)
        {
            while (true)
            {
                if (block < VirtualBlock)
                {
                    if (!MovePrevious()) return false;
                }
                else if (block >= VirtualBlock + CurrentSegmentSize)
                {
                    if (!MoveNext()) return false;
                }
                else
                {
                    return true;
                }
            }
        }
    }
}
