﻿using System;

namespace LibHac.IO.Save
{
    public class AllocationTableStorage : Storage
    {
        private Storage BaseStorage { get; }
        private int BlockSize { get; }
        private int InitialBlock { get; }
        private AllocationTable Fat { get; }

        public override long Length { get; }

        public AllocationTableStorage(Storage data, AllocationTable table, int blockSize, int initialBlock, long length)
        {
            BaseStorage = data;
            BlockSize = blockSize;
            Length = length;
            Fat = table;
            InitialBlock = initialBlock;
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
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

                int bytesRead = BaseStorage.Read(destination.Slice(outPos, bytesToRead), physicalOffset);

                outPos += bytesRead;
                inPos += bytesRead;
                remaining -= bytesRead;
            }

            return outPos;
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

    }
}
