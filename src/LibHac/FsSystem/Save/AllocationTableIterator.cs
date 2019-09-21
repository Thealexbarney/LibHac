using System;

namespace LibHac.FsSystem.Save
{
    public class AllocationTableIterator
    {
        private AllocationTable Fat { get; }

        public int VirtualBlock { get; private set; }
        public int PhysicalBlock { get; private set; }
        public int CurrentSegmentSize => _currentSegmentSize;

        private int _nextBlock;
        private int _prevBlock;
        private int _currentSegmentSize;

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
            PhysicalBlock = initialBlock;
            Fat.ReadEntry(initialBlock, out _nextBlock, out _prevBlock, out _currentSegmentSize);

            return _prevBlock == -1;
        }

        public bool MoveNext()
        {
            if (_nextBlock == -1) return false;

            VirtualBlock += _currentSegmentSize;
            PhysicalBlock = _nextBlock;

            Fat.ReadEntry(_nextBlock, out _nextBlock, out _prevBlock, out _currentSegmentSize);
            
            return true;
        }

        public bool MovePrevious()
        {
            if (_prevBlock == -1) return false;

            PhysicalBlock = _prevBlock;

            Fat.ReadEntry(_prevBlock, out _nextBlock, out _prevBlock, out _currentSegmentSize);

            VirtualBlock -= _currentSegmentSize;
            
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
