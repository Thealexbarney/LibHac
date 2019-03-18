using System;

namespace LibHac.IO.Save
{
    public class AllocationTableStorage : StorageBase
    {
        private IStorage BaseStorage { get; }
        private int BlockSize { get; }
        private int InitialBlock { get; }
        private AllocationTable Fat { get; }

        private long _length;

        public AllocationTableStorage(IStorage data, AllocationTable table, int blockSize, int initialBlock, long length)
        {
            BaseStorage = data;
            BlockSize = blockSize;
            _length = length;
            Fat = table;
            InitialBlock = initialBlock;
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
    }
}
