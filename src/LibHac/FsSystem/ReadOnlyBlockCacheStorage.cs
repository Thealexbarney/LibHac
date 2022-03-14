using System;
using System.Collections.Generic;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Os;
using LibHac.Util;

using BlockCache = LibHac.FsSystem.LruListCache<long, System.Memory<byte>>;

namespace LibHac.FsSystem;

/// <summary>
/// Caches reads to a base <see cref="IStorage"/> using a least-recently-used cache of data blocks.
/// The offset and size read from the storage must be aligned to multiples of the block size.
/// Only reads that access a single block will use the cache. Reads that access multiple blocks will
/// be passed down to the base <see cref="IStorage"/> to be handled without caching.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public class ReadOnlyBlockCacheStorage : IStorage
{
    private SdkMutexType _mutex;
    private BlockCache _blockCache;
    private SharedRef<IStorage> _baseStorage;
    private int _blockSize;

    public ReadOnlyBlockCacheStorage(ref SharedRef<IStorage> baseStorage, int blockSize, Memory<byte> buffer,
        int cacheBlockCount)
    {
        _baseStorage = SharedRef<IStorage>.CreateMove(ref baseStorage);
        _blockSize = blockSize;
        _blockCache = new BlockCache();
        _mutex = new SdkMutexType();

        Assert.SdkRequiresGreaterEqual(buffer.Length, _blockSize);
        Assert.SdkRequires(BitUtil.IsPowerOfTwo(blockSize), $"{nameof(blockSize)} must be power of 2.");
        Assert.SdkRequiresGreater(cacheBlockCount, 0);
        Assert.SdkRequiresGreaterEqual(buffer.Length, blockSize * cacheBlockCount);

        for (int i = 0; i < cacheBlockCount; i++)
        {
            Memory<byte> nodeBuffer = buffer.Slice(i * blockSize, blockSize);
            var node = new LinkedListNode<BlockCache.Node>(new BlockCache.Node(nodeBuffer));
            Assert.SdkNotNull(node);

            _blockCache.PushMruNode(node, -1);
        }
    }

    public override void Dispose()
    {
        _blockCache.DeleteAllNodes();
        _baseStorage.Destroy();

        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        Assert.SdkRequiresAligned(offset, _blockSize);
        Assert.SdkRequiresAligned(destination.Length, _blockSize);

        if (destination.Length == _blockSize)
        {
            // Search the cache for the requested block.
            using (new ScopedLock<SdkMutexType>(ref _mutex))
            {
                bool found = _blockCache.FindValueAndUpdateMru(out Memory<byte> cachedBuffer, offset / _blockSize);
                if (found)
                {
                    cachedBuffer.Span.CopyTo(destination);
                    return Result.Success;
                }
            }

            // The block wasn't in the cache. Read from the base storage.
            Result rc = _baseStorage.Get.Read(offset, destination);
            if (rc.IsFailure()) return rc.Miss();

            // Add the block to the cache.
            using (new ScopedLock<SdkMutexType>(ref _mutex))
            {
                LinkedListNode<BlockCache.Node> lru = _blockCache.PopLruNode();
                destination.CopyTo(lru.ValueRef.Value.Span);
                _blockCache.PushMruNode(lru, offset / _blockSize);
            }

            return Result.Success;
        }
        else
        {
            return _baseStorage.Get.Read(offset, destination);
        }
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        // Missing: Log output
        return ResultFs.UnsupportedWriteForReadOnlyBlockCacheStorage.Log();
    }

    public override Result Flush()
    {
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForReadOnlyBlockCacheStorage.Log();
    }

    public override Result GetSize(out long size)
    {
        return _baseStorage.Get.GetSize(out size);
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        if (operationId == OperationId.InvalidateCache)
        {
            // Invalidate all the blocks in our cache.
            using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

            int cacheBlockCount = _blockCache.GetSize();
            for (int i = 0; i < cacheBlockCount; i++)
            {
                LinkedListNode<BlockCache.Node> lru = _blockCache.PopLruNode();
                _blockCache.PushMruNode(lru, -1);
            }
        }
        else
        {
            Assert.SdkRequiresAligned(offset, _blockSize);
            Assert.SdkRequiresAligned(size, _blockSize);
        }

        // Pass the request to the base storage.
        return _baseStorage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
    }
}