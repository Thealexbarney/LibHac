using System;
using System.Buffers;
using System.Collections.Generic;

namespace LibHac.IO
{
    public class CachedStorage : Storage
    {
        private IStorage BaseStorage { get; }
        private int BlockSize { get; }

        private LinkedList<CacheBlock> Blocks { get; } = new LinkedList<CacheBlock>();
        private Dictionary<long, LinkedListNode<CacheBlock>> BlockDict { get; } = new Dictionary<long, LinkedListNode<CacheBlock>>();

        public CachedStorage(IStorage baseStorage, int blockSize, int cacheSize, bool leaveOpen)
        {
            BaseStorage = baseStorage;
            BlockSize = blockSize;
            Length = BaseStorage.Length;

            if (!leaveOpen) ToDispose.Add(BaseStorage);

            for (int i = 0; i < cacheSize; i++)
            {
                // todo why is this rented?
                var block = new CacheBlock { Buffer = ArrayPool<byte>.Shared.Rent(blockSize) };
                Blocks.AddLast(block);
            }
        }

        public CachedStorage(SectorStorage baseStorage, int cacheSize, bool leaveOpen)
            : this(baseStorage, baseStorage.SectorSize, cacheSize, leaveOpen) { }

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            long remaining = destination.Length;
            long inOffset = offset;
            int outOffset = 0;

            lock (Blocks)
            {
                while (remaining > 0)
                {
                    long blockIndex = inOffset / BlockSize;
                    int blockPos = (int)(inOffset % BlockSize);
                    CacheBlock block = GetBlock(blockIndex);

                    int bytesToRead = (int)Math.Min(remaining, BlockSize - blockPos);

                    block.Buffer.AsSpan(blockPos, bytesToRead).CopyTo(destination.Slice(outOffset));

                    outOffset += bytesToRead;
                    inOffset += bytesToRead;
                    remaining -= bytesToRead;
                }
            }
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            long remaining = source.Length;
            long inOffset = offset;
            int outOffset = 0;

            lock (Blocks)
            {
                while (remaining > 0)
                {
                    long blockIndex = inOffset / BlockSize;
                    int blockPos = (int)(inOffset % BlockSize);
                    CacheBlock block = GetBlock(blockIndex);

                    int bytesToWrite = (int)Math.Min(remaining, BlockSize - blockPos);

                    source.Slice(outOffset, bytesToWrite).CopyTo(block.Buffer.AsSpan(blockPos));

                    block.Dirty = true;

                    outOffset += bytesToWrite;
                    inOffset += bytesToWrite;
                    remaining -= bytesToWrite;
                }
            }
        }

        public override void Flush()
        {
            lock (Blocks)
            {
                foreach (CacheBlock cacheItem in Blocks)
                {
                    FlushBlock(cacheItem);
                }
            }

            BaseStorage.Flush();
        }

        public override long Length { get; }

        private CacheBlock GetBlock(long blockIndex)
        {
            if (BlockDict.TryGetValue(blockIndex, out LinkedListNode<CacheBlock> node))
            {
                if (Blocks.First != node)
                {
                    Blocks.Remove(node);
                    Blocks.AddFirst(node);
                }

                return node.Value;
            }

            node = Blocks.Last;
            FlushBlock(node.Value);

            CacheBlock block = node.Value;
            Blocks.RemoveLast();
            BlockDict.Remove(block.Index);

            FlushBlock(block);
            ReadBlock(block, blockIndex);

            Blocks.AddFirst(node);
            BlockDict.Add(blockIndex, node);

            return block;
        }

        private void ReadBlock(CacheBlock block, long index)
        {
            long offset = index * BlockSize;
            int length = BlockSize;

            if (Length != -1)
            {
                length = (int)Math.Min(Length - offset, length);
            }

            BaseStorage.Read(block.Buffer.AsSpan(0, length), offset);
            block.Length = length;
            block.Index = index;
            block.Dirty = false;
        }

        private void FlushBlock(CacheBlock block)
        {
            if (!block.Dirty) return;

            long offset = block.Index * BlockSize;
            BaseStorage.Write(block.Buffer, offset, block.Length, 0);
            block.Dirty = false;
        }

        private class CacheBlock
        {
            public long Index { get; set; }
            public byte[] Buffer { get; set; }
            public int Length { get; set; }
            public bool Dirty { get; set; }
        }
    }
}
