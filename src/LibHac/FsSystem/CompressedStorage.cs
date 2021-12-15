using System;
using System.Runtime.CompilerServices;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem.Impl;
using LibHac.Os;
using Buffer = LibHac.Mem.Buffer;

namespace LibHac.FsSystem;

public class CompressedStorage : IStorage, IAsynchronousAccessSplitter
{
    public delegate Result DecompressorFunction(Span<byte> destination, ReadOnlySpan<byte> source);
    public delegate DecompressorFunction GetDecompressorFunction(CompressionType type);

    public class CompressedStorageCore : IDisposable
    {
        private long _blockSizeMax;
        private long _continuousReadingSizeMax;
        private readonly BucketTree _bucketTree;
        private ValueSubStorage _dataStorage;
        private GetDecompressorFunction _getDecompressorFunction;

        public CompressedStorageCore()
        {
            _bucketTree = new BucketTree();
            _dataStorage = new ValueSubStorage();
        }

        public void Dispose()
        {
            FinalizeObject();
            _dataStorage.Dispose();
            _bucketTree.Dispose();
        }

        private bool IsInitialized()
        {
            return _bucketTree.IsInitialized();
        }

        public Result GetSize(out long size)
        {
            Assert.SdkRequiresNotNullOut(out size);

            Result rc = _bucketTree.GetOffsets(out BucketTree.Offsets offsets);
            if (rc.IsFailure()) return rc.Miss();

            size = offsets.EndOffset;
            return Result.Success;
        }

        public delegate Result OperatePerEntryFunc(out bool isContinuous, in Entry entry, long virtualDataSize,
            long offsetInEntry, long readSize);

        public Result OperatePerEntry(long offset, long size, OperatePerEntryFunc func)
        {
            throw new NotImplementedException();
        }

        public delegate Result OperateEntryFunc(long offset, long size);

        public Result OperateEntry(long offset, long size, OperatePerEntryFunc func)
        {
            throw new NotImplementedException();
        }

        public Result Initialize(MemoryResource allocatorForBucketTree, in ValueSubStorage dataStorage,
            in ValueSubStorage nodeStorage, in ValueSubStorage entryStorage, int bucketTreeEntryCount,
            long blockSizeMax, long continuousReadingSizeMax, GetDecompressorFunction getDecompressorFunc)
        {
            Assert.SdkRequiresNotNull(allocatorForBucketTree);
            Assert.SdkRequiresLess(0, blockSizeMax);
            Assert.SdkRequiresLessEqual(blockSizeMax, continuousReadingSizeMax);
            Assert.SdkRequiresNotNull(getDecompressorFunc);

            Result rc = _bucketTree.Initialize(allocatorForBucketTree, in nodeStorage, in entryStorage, NodeSize,
                Unsafe.SizeOf<Entry>(), bucketTreeEntryCount);
            if (rc.IsFailure()) return rc.Miss();

            _blockSizeMax = blockSizeMax;
            _continuousReadingSizeMax = continuousReadingSizeMax;
            _dataStorage.Set(in dataStorage);
            _getDecompressorFunction = getDecompressorFunc;

            return Result.Success;
        }

        public void FinalizeObject()
        {
            if (IsInitialized())
            {
                _bucketTree.FinalizeObject();

                using var temp = new ValueSubStorage();
                _dataStorage.Set(in temp);
            }
        }

        public delegate Result ReadImplFunc(Span<byte> buffer);
        public delegate Result ReadFunc(long sizeBufferRequired, ReadImplFunc readImplFunc);

        public Result Read(long offset, long size, ReadFunc func)
        {
            throw new NotImplementedException();
        }

        public Result Invalidate()
        {
            throw new NotImplementedException();
        }

        public Result QueryRange(Span<byte> buffer, long offset, long size)
        {
            throw new NotImplementedException();
        }

        public Result QueryAppropriateOffsetForAsynchronousAccess(out long offsetAppropriate, long offset,
            long accessSize, long alignmentSize)
        {
            throw new NotImplementedException();
        }

        private DecompressorFunction GetDecompressor(CompressionType type)
        {
            if (CompressionTypeUtility.IsUnknownType(type))
                return null;

            return _getDecompressorFunction(type);
        }
    }

    public class CacheManager : IDisposable
    {
        public struct CacheEntry : IBlockCacheManagerEntry<Range>
        {
            public Range Range { get; set; }
            public long Handle { get; set; }
            public Buffer Buffer { get; set; }
            public bool IsValid { get; set; }
            public bool IsCached { get; set; }
            public short Age { get; set; }

            public void Invalidate() { /* empty */ }
            public readonly bool IsAllocated() => IsValid && Handle != 0;


            public bool IsWriteBack
            {
                get => false;
                set { }
            }

            public bool IsFlushing
            {
                set { }
            }
        }

        public struct Range : IBlockCacheManagerRange
        {
            public long Offset { get; set; }
            public uint Size { get; set; }

            public long GetEndOffset() => Offset + Size;
            public bool IsIncluded(long offset) => Offset <= offset && offset < GetEndOffset();
        }

        public struct AccessRange
        {
            public long VirtualOffset;
            public long VirtualSize;
            public uint PhysicalSize;
            public bool IsBlockAlignmentRequired;

            public readonly long GetEndVirtualOffset() => VirtualOffset + VirtualSize;
        }

        private long _cacheSize0;
        private long _cacheSize1;
        private SdkMutexType _mutex;
        private BlockCacheManager<CacheEntry, Range> _cacheManager;
        private long _storageSize;

        public CacheManager()
        {
            _mutex = new SdkMutexType();
            _cacheManager = new BlockCacheManager<CacheEntry, Range>();
            _storageSize = 0;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Result Initialize(IBufferManager allocator, long storageSize, long cacheSize0, long cacheSize1,
            int maxCacheEntries)
        {
            Result rc = _cacheManager.Initialize(allocator, maxCacheEntries);
            if (rc.IsFailure()) return rc.Miss();

            _storageSize = storageSize;
            _cacheSize0 = cacheSize0;
            _cacheSize1 = cacheSize1;

            return Result.Success;
        }

        public void FinalizeObject()
        {
            throw new NotImplementedException();
        }

        public void Invalidate()
        {
            throw new NotImplementedException();
        }

        public Result Read(CompressedStorageCore core, long offset, Span<byte> buffer)
        {
            throw new NotImplementedException();
        }

        private Result FindBufferImpl(out Buffer buffer, out CacheEntry entry, long offset)
        {
            throw new NotImplementedException();
        }

        private Result FindBuffer(out Buffer buffer, out CacheEntry entry, long offset)
        {
            throw new NotImplementedException();
        }

        private Result FindOrAllocateBuffer(out Buffer buffer, out CacheEntry entry, long offset, ulong size)
        {
            throw new NotImplementedException();
        }

        private void StoreAssociateBuffer(Buffer buffer, in CacheEntry entry)
        {
            throw new NotImplementedException();
        }

        private Result ReadHeadCache(CompressedStorageCore core, ref long offset, ref Span<byte> buffer,
            ref AccessRange headRange, in AccessRange endRange)
        {
            throw new NotImplementedException();
        }

        private Result ReadTailCache(CompressedStorageCore core, long offset, Span<byte> buffer,
            in AccessRange headRange, ref AccessRange endRange)
        {
            throw new NotImplementedException();
        }
    }

    public struct Entry
    {
        public long VirtualOffset;
        public long PhysicalOffset;
        public CompressionType CompressionType;
        public uint PhysicalSize;

        public readonly long GetPhysicalSize() => PhysicalSize;
    }

    public static readonly int NodeSize = 0x4000;

    private CompressedStorageCore _core;
    private CacheManager _cacheManager;

    public static long QueryEntryStorageSize(int entryCount)
    {
        return BucketTree.QueryEntryStorageSize(NodeSize, Unsafe.SizeOf<Entry>(), entryCount);
    }

    public static long QueryNodeStorageSize(int entryCount)
    {
        return BucketTree.QueryNodeStorageSize(NodeSize, Unsafe.SizeOf<Entry>(), entryCount);
    }

    public CompressedStorage()
    {
        _core = new CompressedStorageCore();
        _cacheManager = new CacheManager();
    }

    public override void Dispose()
    {
        FinalizeObject();
        _cacheManager.Dispose();
        _core.Dispose();
    }

    public Result Initialize(MemoryResource allocatorForBucketTree, IBufferManager allocatorForCacheManager,
        in ValueSubStorage dataStorage, in ValueSubStorage nodeStorage, in ValueSubStorage entryStorage,
        int bucketTreeEntryCount, long blockSizeMax, long continuousReadingSizeMax,
        GetDecompressorFunction getDecompressorFunc, long cacheSize0, long cacheSize1, int maxCacheEntries)
    {
        Result rc = _core.Initialize(allocatorForBucketTree, in dataStorage, in nodeStorage, in entryStorage,
            bucketTreeEntryCount, blockSizeMax, continuousReadingSizeMax, getDecompressorFunc);
        if (rc.IsFailure()) return rc.Miss();

        rc = _core.GetSize(out long size);
        if (rc.IsFailure()) return rc.Miss();

        rc = _cacheManager.Initialize(allocatorForCacheManager, size, cacheSize0, cacheSize1, maxCacheEntries);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;

    }

    public void FinalizeObject()
    {
        _cacheManager.FinalizeObject();
        _core.FinalizeObject();
    }

    protected override Result DoRead(long offset, Span<byte> destination)
    {
        Result rc = _cacheManager.Read(_core, offset, destination);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
    {
        return ResultFs.UnsupportedWriteForCompressedStorage.Log();
    }

    protected override Result DoFlush()
    {
        return Result.Success;
    }

    protected override Result DoSetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForIndirectStorage.Log();
    }

    protected override Result DoGetSize(out long size)
    {
        Result rc = _core.GetSize(out size);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        Assert.SdkRequiresLessEqual(0, offset);
        Assert.SdkRequiresLessEqual(0, size);

        switch (operationId)
        {
            case OperationId.InvalidateCache:
                {
                    _cacheManager.Invalidate();
                    Result rc = _core.Invalidate();
                    if (rc.IsFailure()) return rc.Miss();
                    break;
                }
            case OperationId.QueryRange:
                {
                    Result rc = _core.QueryRange(outBuffer, offset, size);
                    if (rc.IsFailure()) return rc.Miss();
                    break;
                }
            default:
                return ResultFs.UnsupportedOperateRangeForCompressedStorage.Log();
        }

        return Result.Success;
    }

    public Result QueryAppropriateOffset(out long offsetAppropriate, long startOffset, long accessSize,
        long alignmentSize)
    {
        Result rc = _core.QueryAppropriateOffsetForAsynchronousAccess(out offsetAppropriate, startOffset, accessSize,
            alignmentSize);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }
}