using System;
using System.Collections.Generic;

namespace LibHac.Fs
{
    public class CachedStorage : StorageBase
    {
        private IStorage BaseStorage { get; }
        private int BlockSize { get; }
        private long _length;

        private LinkedList<CacheBlock> Blocks { get; } = new LinkedList<CacheBlock>();
        private Dictionary<long, LinkedListNode<CacheBlock>> BlockDict { get; } = new Dictionary<long, LinkedListNode<CacheBlock>>();

        public CachedStorage(IStorage baseStorage, int blockSize, int cacheSize, bool leaveOpen)
        {
            BaseStorage = baseStorage;
            BlockSize = blockSize;
            BaseStorage.GetSize(out _length).ThrowIfFailure();

            if (!leaveOpen) ToDispose.Add(BaseStorage);

            for (int i = 0; i < cacheSize; i++)
            {
                var block = new CacheBlock { Buffer = new byte[blockSize], Index = -1 };
                Blocks.AddLast(block);
            }
        }

        public CachedStorage(SectorStorage baseStorage, int cacheSize, bool leaveOpen)
            : this(baseStorage, baseStorage.SectorSize, cacheSize, leaveOpen) { }

        protected override Result ReadImpl(long offset, Span<byte> destination)
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

            return Result.Success;
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
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

            return Result.Success;
        }

        public override Result Flush()
        {
            lock (Blocks)
            {
                foreach (CacheBlock cacheItem in Blocks)
                {
                    FlushBlock(cacheItem);
                }
            }

            return BaseStorage.Flush();
        }

        public override Result GetSize(out long size)
        {
            size = _length;
            return Result.Success;
        }

        public override Result SetSize(long size)
        {
            Result rc = BaseStorage.SetSize(size);
            if (rc.IsFailure()) return rc;

            rc = BaseStorage.GetSize(out long newSize);
            if (rc.IsFailure()) return rc;

            _length = newSize;

            return Result.Success;
        }

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

            if (block.Index != -1)
            {
                BlockDict.Remove(block.Index);
            }

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

            if (_length != -1)
            {
                length = (int)Math.Min(_length - offset, length);
            }

            BaseStorage.Read(offset, block.Buffer.AsSpan(0, length)).ThrowIfFailure();
            block.Length = length;
            block.Index = index;
            block.Dirty = false;
        }

        private void FlushBlock(CacheBlock block)
        {
            if (!block.Dirty) return;

            long offset = block.Index * BlockSize;
            BaseStorage.Write(offset, block.Buffer.AsSpan(0, block.Length)).ThrowIfFailure();
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
