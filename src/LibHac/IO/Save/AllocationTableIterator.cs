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
                throw new ArgumentException($"Attempted to start FAT iteration from an invalid block. ({initialBlock})");
            }
        }

        public bool BeginIteration(int initialBlock)
        {
            Fat.ReadEntry(initialBlock + 1, out AllocationTableEntry tableEntry);

            if (!tableEntry.IsListStart() && initialBlock != -1)
            {
                return false;
            }

            if (tableEntry.IsSingleBlockSegment())
            {
                CurrentSegmentSize = 1;
            }
            else
            {
                Fat.ReadEntry(initialBlock + 2, out AllocationTableEntry lengthEntry);
                CurrentSegmentSize = lengthEntry.Next - initialBlock;
            }

            PhysicalBlock = initialBlock;

            return true;
        }

        public bool MoveNext()
        {
            Fat.ReadEntry(PhysicalBlock + 1, out AllocationTableEntry currentEntry);
            if (currentEntry.IsListEnd()) return false;
            int newBlock = currentEntry.Next & 0x7FFFFFFF;

            Fat.ReadEntry(newBlock, out AllocationTableEntry newEntry);
            VirtualBlock += CurrentSegmentSize;

            if (newEntry.IsSingleBlockSegment())
            {
                CurrentSegmentSize = 1;
            }
            else
            {
                Fat.ReadEntry(newBlock + 1, out AllocationTableEntry lengthEntry);
                CurrentSegmentSize = lengthEntry.Next - (newBlock - 1);
            }

            PhysicalBlock = newBlock - 1;
            return true;
        }

        public bool MovePrevious()
        {
            Fat.ReadEntry(PhysicalBlock + 1, out AllocationTableEntry currentEntry);
            if (currentEntry.IsListStart()) return false;
            int newBlock = currentEntry.Prev & 0x7FFFFFFF;

            Fat.ReadEntry(newBlock, out AllocationTableEntry newEntry);

            if (newEntry.IsSingleBlockSegment())
            {
                CurrentSegmentSize = 1;
            }
            else
            {
                Fat.ReadEntry(newBlock + 1, out AllocationTableEntry lengthEntry);
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
