using System;
using System.Buffers.Binary;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using Xunit;

namespace LibHac.Tests.FsSystem;

public class ReadOnlyBlockCacheStorageTests
{
    private class TestContext
    {
        private int _blockSize;
        private int _cacheBlockCount;

        public byte[] BaseData;
        public byte[] ModifiedBaseData;
        public byte[] CacheBuffer;

        public ReadOnlyBlockCacheStorage CacheStorage;

        public TestContext(int blockSize, int cacheBlockCount, int storageBlockCount, ulong rngSeed)
        {
            _blockSize = blockSize;
            _cacheBlockCount = cacheBlockCount;

            BaseData = new byte[_blockSize * storageBlockCount];
            ModifiedBaseData = new byte[_blockSize * storageBlockCount];
            CacheBuffer = new byte[_blockSize * _cacheBlockCount];

            new Random(rngSeed).NextBytes(BaseData);
            BaseData.AsSpan().CopyTo(ModifiedBaseData);

            for (int i = 0; i < storageBlockCount; i++)
            {
                ModifyBlock(GetModifiedBaseDataBlock(i));
            }

            using var baseStorage = new SharedRef<IStorage>(new MemoryStorage(BaseData));
            CacheStorage = new ReadOnlyBlockCacheStorage(in baseStorage, _blockSize, CacheBuffer, _cacheBlockCount);
        }

        public Span<byte> GetBaseDataBlock(int index) => BaseData.AsSpan(_blockSize * index, _blockSize);
        public Span<byte> GetModifiedBaseDataBlock(int index) => ModifiedBaseData.AsSpan(_blockSize * index, _blockSize);
        public Span<byte> GetCacheDataBlock(int index) => CacheBuffer.AsSpan(_blockSize * index, _blockSize);

        private void ModifyBlock(Span<byte> block)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(block, ulong.MaxValue);
        }

        public void ModifyAllCacheBlocks()
        {
            for (int i = 0; i < _cacheBlockCount; i++)
            {
                ModifyBlock(GetCacheDataBlock(i));
            }
        }

        public Span<byte> ReadCachedStorage(int blockIndex)
        {
            byte[] buffer = new byte[_blockSize];
            Assert.Success(CacheStorage.Read(_blockSize * blockIndex, buffer));
            return buffer;
        }

        public Span<byte> ReadCachedStorage(long offset, int size)
        {
            byte[] buffer = new byte[size];
            Assert.Success(CacheStorage.Read(offset, buffer));
            return buffer;
        }

        public void InvalidateCache()
        {
            Assert.Success(CacheStorage.OperateRange(OperationId.InvalidateCache, 0, long.MaxValue));
        }
    }

    private const int BlockSize = 0x4000;
    private const int CacheBlockCount = 4;
    private const int StorageBlockCount = 16;

    [Fact]
    public void Read_CompleteBlocks_ReadsCorrectData()
    {
        var context = new TestContext(BlockSize, CacheBlockCount, StorageBlockCount, 21341);

        for (int i = 0; i < StorageBlockCount; i++)
        {
            Assert.True(context.GetBaseDataBlock(i).SequenceEqual(context.ReadCachedStorage(i)));
            Assert.True(context.GetBaseDataBlock(i).SequenceEqual(context.ReadCachedStorage(i)));
        }
    }

    [Fact]
    public void Read_PreviouslyCachedBlock_ReturnsDataFromCache()
    {
        const int index = 4;
        var context = new TestContext(BlockSize, CacheBlockCount, StorageBlockCount, 21341);

        // Cache the block
        context.ReadCachedStorage(index);

        // Directly modify the cache buffer
        context.ModifyAllCacheBlocks();

        // Next read should return the modified data from the cache buffer
        Assert.True(context.GetModifiedBaseDataBlock(index).SequenceEqual(context.ReadCachedStorage(index)));
    }

    [Fact]
    public void Read_BlockEvictedFromCache_ReturnsDataFromBaseStorage()
    {
        const int index = 4;
        var context = new TestContext(BlockSize, CacheBlockCount, StorageBlockCount, 21341);

        context.ReadCachedStorage(index);
        context.ModifyAllCacheBlocks();

        // Read enough additional blocks to push the initial block out of the cache
        context.ReadCachedStorage(6);
        context.ReadCachedStorage(7);
        context.ReadCachedStorage(8);
        context.ReadCachedStorage(9);

        // Reading the initial block should now return the original data
        Assert.True(context.GetBaseDataBlock(index).SequenceEqual(context.ReadCachedStorage(index)));
    }

    [Fact]
    public void Read_ReadMultipleBlocks_BlocksAreEvictedAtTheRightTime()
    {
        const int index = 4;
        var context = new TestContext(BlockSize, CacheBlockCount, StorageBlockCount, 21341);

        context.ReadCachedStorage(index);
        context.ModifyAllCacheBlocks();

        context.ReadCachedStorage(6);
        context.ReadCachedStorage(7);
        context.ReadCachedStorage(8);

        // Reading the initial block should return the cached data
        Assert.True(context.GetModifiedBaseDataBlock(index).SequenceEqual(context.ReadCachedStorage(index)));

        for (int i = 0; i < 3; i++)
            context.ReadCachedStorage(9 + i);

        context.ModifyAllCacheBlocks();

        // The initial block should have been moved to the top of the cache when it was last accessed
        Assert.True(context.GetModifiedBaseDataBlock(index).SequenceEqual(context.ReadCachedStorage(index)));

        // Access all the other blocks in the cache so the initial block is the least recently accessed
        for (int i = 0; i < 3; i++)
            Assert.True(context.GetModifiedBaseDataBlock(9 + i).SequenceEqual(context.ReadCachedStorage(9 + i)));

        // Add a new block to the cache
        Assert.True(context.GetBaseDataBlock(2).SequenceEqual(context.ReadCachedStorage(2)));

        // The initial block should have been removed from the cache
        Assert.True(context.GetBaseDataBlock(index).SequenceEqual(context.ReadCachedStorage(index)));
    }

    [Fact]
    public void Read_UnalignedBlock_ReturnsOriginalData()
    {
        const int index = 4;
        var context = new TestContext(BlockSize, CacheBlockCount, StorageBlockCount, 21341);

        context.ReadCachedStorage(index);
        context.ModifyAllCacheBlocks();

        // Read two blocks at once
        int offset = index * BlockSize;
        int size = BlockSize * 2;

        // The cache should be bypassed, returning the original data
        Assert.True(context.BaseData.AsSpan(offset, size).SequenceEqual(context.ReadCachedStorage(offset, size)));
    }

    [Fact]
    public void OperateRange_InvalidateCache_PreviouslyCachedBlockReturnsDataFromBaseStorage()
    {
        const int index = 4;
        var context = new TestContext(BlockSize, CacheBlockCount, StorageBlockCount, 21341);

        context.ReadCachedStorage(index);
        context.ModifyAllCacheBlocks();

        // Next read should return the modified data from the cache buffer
        Assert.True(context.GetModifiedBaseDataBlock(index).SequenceEqual(context.ReadCachedStorage(index)));

        // Reading after invalidating the cache should return the original data
        context.InvalidateCache();
        Assert.True(context.GetBaseDataBlock(index).SequenceEqual(context.ReadCachedStorage(index)));
    }
}