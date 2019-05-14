using System;
using System.IO;

namespace LibHac.Fs.Save
{
    public class AllocationTableStorage : StorageBase
    {
        private IStorage BaseStorage { get; }
        private int BlockSize { get; }
        internal int InitialBlock { get; private set; }
        private AllocationTable Fat { get; }

        private long _length;

        public AllocationTableStorage(IStorage data, AllocationTable table, int blockSize, int initialBlock)
        {
            BaseStorage = data;
            BlockSize = blockSize;
            Fat = table;
            InitialBlock = initialBlock;

            _length = initialBlock == -1 ? 0 : table.GetListLength(initialBlock) * blockSize;
        }

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            var iterator = new AllocationTableIterator(Fat, InitialBlock);

            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                int blockNum = (int)(inPos / BlockSize);
                iterator.Seek(blockNum);

                int segmentPos = (int)(inPos - (long)iterator.VirtualBlock * BlockSize);
                long physicalOffset = iterator.PhysicalBlock * BlockSize + segmentPos;

                int remainingInSegment = iterator.CurrentSegmentSize * BlockSize - segmentPos;
                int bytesToRead = Math.Min(remaining, remainingInSegment);

                BaseStorage.Read(destination.Slice(outPos, bytesToRead), physicalOffset);

                outPos += bytesToRead;
                inPos += bytesToRead;
                remaining -= bytesToRead;
            }
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            var iterator = new AllocationTableIterator(Fat, InitialBlock);

            long inPos = offset;
            int outPos = 0;
            int remaining = source.Length;

            while (remaining > 0)
            {
                int blockNum = (int)(inPos / BlockSize);
                iterator.Seek(blockNum);

                int segmentPos = (int)(inPos - (long)iterator.VirtualBlock * BlockSize);
                long physicalOffset = iterator.PhysicalBlock * BlockSize + segmentPos;

                int remainingInSegment = iterator.CurrentSegmentSize * BlockSize - segmentPos;
                int bytesToWrite = Math.Min(remaining, remainingInSegment);

                BaseStorage.Write(source.Slice(outPos, bytesToWrite), physicalOffset);

                outPos += bytesToWrite;
                inPos += bytesToWrite;
                remaining -= bytesToWrite;
            }
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }

        public override long GetSize() => _length;

        public override void SetSize(long size)
        {
            int oldBlockCount = (int)Util.DivideByRoundUp(_length, BlockSize);
            int newBlockCount = (int)Util.DivideByRoundUp(size, BlockSize);

            if (oldBlockCount == newBlockCount) return;

            if (oldBlockCount == 0)
            {
                InitialBlock = Fat.Allocate(newBlockCount);
                if (InitialBlock == -1) throw new IOException("Not enough space to resize file.");

                _length = newBlockCount * BlockSize;

                return;
            }

            if (newBlockCount == 0)
            {
                Fat.Free(InitialBlock);

                InitialBlock = int.MinValue;
                _length = 0;

                return;
            }

            if (newBlockCount > oldBlockCount)
            {
                int newBlocks = Fat.Allocate(newBlockCount - oldBlockCount);
                if (InitialBlock == -1) throw new IOException("Not enough space to resize file.");

                Fat.Join(InitialBlock, newBlocks);
            }
            else
            {
                int oldBlocks = Fat.Trim(InitialBlock, newBlockCount);
                Fat.Free(oldBlocks);
            }

            _length = newBlockCount * BlockSize;
        }
    }
}
