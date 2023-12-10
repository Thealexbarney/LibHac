// ReSharper disable UnusedMember.Local NotAccessedField.Local
using System;
using System.Runtime.CompilerServices;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem.Impl;
using LibHac.Os;
using Buffer = LibHac.Mem.Buffer;
using CacheHandle = System.UInt64;

namespace LibHac.FsSystem;

public class CompressedStorage : IStorage, IAsynchronousAccessSplitter
{
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

            Result res = _bucketTree.GetOffsets(out BucketTree.Offsets offsets);
            if (res.IsFailure()) return res.Miss();

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

        public Result Initialize(MemoryResource allocatorForBucketTree, ref readonly ValueSubStorage dataStorage,
            ref readonly ValueSubStorage nodeStorage, ref readonly ValueSubStorage entryStorage,
            int bucketTreeEntryCount, long blockSizeMax, long continuousReadingSizeMax,
            GetDecompressorFunction getDecompressorFunc)
        {
            Assert.SdkRequiresNotNull(allocatorForBucketTree);
            Assert.SdkRequiresLess(0, blockSizeMax);
            Assert.SdkRequiresLessEqual(blockSizeMax, continuousReadingSizeMax);
            Assert.SdkRequiresNotNull(getDecompressorFunc);

            Result res = _bucketTree.Initialize(allocatorForBucketTree, in nodeStorage, in entryStorage, NodeSize,
                Unsafe.SizeOf<Entry>(), bucketTreeEntryCount);
            if (res.IsFailure()) return res.Miss();

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
            public CacheHandle Handle { get; set; }
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

            public readonly long GetEndOffset() => Offset + Size;
            public readonly bool IsIncluded(long offset) => Offset <= offset && offset < GetEndOffset();
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
            Result res = _cacheManager.Initialize(allocator, maxCacheEntries);
            if (res.IsFailure()) return res.Miss();

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
        public sbyte CompressionLevel;
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

        base.Dispose();
    }

    public Result Initialize(MemoryResource allocatorForBucketTree, IBufferManager allocatorForCacheManager,
        ref readonly ValueSubStorage dataStorage, ref readonly ValueSubStorage nodeStorage,
        ref readonly ValueSubStorage entryStorage, int bucketTreeEntryCount, long blockSizeMax,
        long continuousReadingSizeMax, GetDecompressorFunction getDecompressorFunc, long cacheSize0, long cacheSize1,
        int maxCacheEntries)
    {
        Result res = _core.Initialize(allocatorForBucketTree, in dataStorage, in nodeStorage, in entryStorage,
            bucketTreeEntryCount, blockSizeMax, continuousReadingSizeMax, getDecompressorFunc);
        if (res.IsFailure()) return res.Miss();

        res = _core.GetSize(out long size);
        if (res.IsFailure()) return res.Miss();

        res = _cacheManager.Initialize(allocatorForCacheManager, size, cacheSize0, cacheSize1, maxCacheEntries);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public void FinalizeObject()
    {
        _cacheManager.FinalizeObject();
        _core.FinalizeObject();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        Result res = _cacheManager.Read(_core, offset, destination);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        return ResultFs.UnsupportedWriteForCompressedStorage.Log();
    }

    public override Result Flush()
    {
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForIndirectStorage.Log();
    }

    public override Result GetSize(out long size)
    {
        Result res = _core.GetSize(out size);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        Assert.SdkRequiresLessEqual(0, offset);
        Assert.SdkRequiresLessEqual(0, size);

        switch (operationId)
        {
            case OperationId.InvalidateCache:
            {
                _cacheManager.Invalidate();
                Result res = _core.Invalidate();
                if (res.IsFailure()) return res.Miss();
                break;
            }
            case OperationId.QueryRange:
            {
                Result res = _core.QueryRange(outBuffer, offset, size);
                if (res.IsFailure()) return res.Miss();
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
        Result res = _core.QueryAppropriateOffsetForAsynchronousAccess(out offsetAppropriate, startOffset, accessSize,
            alignmentSize);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}