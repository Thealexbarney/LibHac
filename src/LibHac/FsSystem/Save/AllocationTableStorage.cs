using System;
using System.IO;
using LibHac.Fs;
using LibHac.Util;

namespace LibHac.FsSystem.Save
{
    public class AllocationTableStorage : IStorage
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

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            var iterator = new AllocationTableIterator(Fat, InitialBlock);

            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                int blockNum = (int)(inPos / BlockSize);

                if (!iterator.Seek(blockNum))
                {
                    return ResultFs.InvalidAllocationTableOffset.Log();
                }

                int segmentPos = (int)(inPos - (long)iterator.VirtualBlock * BlockSize);
                long physicalOffset = iterator.PhysicalBlock * BlockSize + segmentPos;

                int remainingInSegment = iterator.CurrentSegmentSize * BlockSize - segmentPos;
                int bytesToRead = Math.Min(remaining, remainingInSegment);

                Result rc = BaseStorage.Read(physicalOffset, destination.Slice(outPos, bytesToRead));
                if (rc.IsFailure()) return rc;

                outPos += bytesToRead;
                inPos += bytesToRead;
                remaining -= bytesToRead;
            }

            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            var iterator = new AllocationTableIterator(Fat, InitialBlock);

            long inPos = offset;
            int outPos = 0;
            int remaining = source.Length;

            while (remaining > 0)
            {
                int blockNum = (int)(inPos / BlockSize);

                if (!iterator.Seek(blockNum))
                {
                    return ResultFs.InvalidAllocationTableOffset.Log();
                }

                int segmentPos = (int)(inPos - (long)iterator.VirtualBlock * BlockSize);
                long physicalOffset = iterator.PhysicalBlock * BlockSize + segmentPos;

                int remainingInSegment = iterator.CurrentSegmentSize * BlockSize - segmentPos;
                int bytesToWrite = Math.Min(remaining, remainingInSegment);

                Result rc = BaseStorage.Write(physicalOffset, source.Slice(outPos, bytesToWrite));
                if (rc.IsFailure()) return rc;

                outPos += bytesToWrite;
                inPos += bytesToWrite;
                remaining -= bytesToWrite;
            }

            return Result.Success;
        }

        protected override Result DoFlush()
        {
            return BaseStorage.Flush();
        }

        protected override Result DoGetSize(out long size)
        {
            size = _length;
            return Result.Success;
        }

        protected override Result DoSetSize(long size)
        {
            int oldBlockCount = (int)BitUtil.DivideUp(_length, BlockSize);
            int newBlockCount = (int)BitUtil.DivideUp(size, BlockSize);

            if (oldBlockCount == newBlockCount) return Result.Success;

            if (oldBlockCount == 0)
            {
                InitialBlock = Fat.Allocate(newBlockCount);
                if (InitialBlock == -1) throw new IOException("Not enough space to resize file.");

                _length = newBlockCount * BlockSize;

                return Result.Success;
            }

            if (newBlockCount == 0)
            {
                Fat.Free(InitialBlock);

                InitialBlock = int.MinValue;
                _length = 0;

                return Result.Success;
            }

            if (newBlockCount > oldBlockCount)
            {
                int newBlocks = Fat.Allocate(newBlockCount - oldBlockCount);
                if (newBlocks == -1) throw new IOException("Not enough space to resize file.");

                Fat.Join(InitialBlock, newBlocks);
            }
            else
            {
                int oldBlocks = Fat.Trim(InitialBlock, newBlockCount);
                Fat.Free(oldBlocks);
            }

            _length = newBlockCount * BlockSize;

            return Result.Success;
        }
    }
}
