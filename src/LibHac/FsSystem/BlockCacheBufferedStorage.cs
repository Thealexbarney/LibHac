using System;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem.Impl;
using LibHac.Os;
using Buffer = LibHac.Mem.Buffer;

// Todo: Remove warning suppressions after implementing
// ReSharper disable All
#pragma warning disable CS0414

namespace LibHac.FsSystem;

public class BlockCacheBufferedStorage : IStorage
{
    public struct CacheEntry : IBlockCacheManagerEntry<AccessRange>
    {
        public AccessRange Range { get; }
        public bool IsValid { get; set; }
        public bool IsWriteBack { get; set; }
        public bool IsCached { get; set; }
        public bool IsFlushing { get; set; }
        public short Age { get; set; }
        public ulong Handle { get; set; }
        public Buffer Buffer { get; set; }

        public void Invalidate()
        {
            throw new NotImplementedException();
        }

        public readonly bool IsAllocated()
        {
            throw new NotImplementedException();
        }
    }

    public struct AccessRange : IBlockCacheManagerRange
    {
        public long Offset { get; set; }
        public long Size { get; set; }

        public readonly long GetEndOffset()
        {
            throw new NotImplementedException();
        }

        public readonly long IsIncluded(long offset)
        {
            throw new NotImplementedException();
        }
    }

    private SdkRecursiveMutex _mutex;
    private IStorage _storageData;
    private Result _lastResult;
    private long _sizeBytesData;
    private int _sizeBytesVerificationBlock;
    private int _shiftBytesVerificationBlock;
    private int _flags;
    private int _bufferLevel;
    private BlockCacheManager<CacheEntry, AccessRange> _cacheManager;
    private bool _isWritable;

    public BlockCacheBufferedStorage()
    {
        _bufferLevel = -1;
        _cacheManager = new BlockCacheManager<CacheEntry, AccessRange>();
    }

    public override void Dispose()
    {
        FinalizeObject();
        _cacheManager.Dispose();

        base.Dispose();
    }

    public bool IsEnabledKeepBurstMode()
    {
        throw new NotImplementedException();
    }

    public void SetKeepBurstMode(bool isEnabled)
    {
        throw new NotImplementedException();
    }

    public void SetRealDataCache(bool isEnabled)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(IBufferManager bufferManager, SdkRecursiveMutex mutex, IStorage data, long dataSize,
        int sizeBytesVerificationBlock, int maxCacheEntries, bool useRealDataCache, sbyte bufferLevel,
        bool useKeepBurstMode, bool isWritable)
    {
        Assert.SdkNotNull(data);
        Assert.SdkNotNull(mutex);
        Assert.SdkNull(_mutex);
        Assert.SdkNull(_storageData);
        Assert.SdkGreater(maxCacheEntries, 0);

        Result res = _cacheManager.Initialize(bufferManager, maxCacheEntries);
        if (res.IsFailure()) return res.Miss();

        _mutex = mutex;
        _storageData = data;
        _sizeBytesData = dataSize;
        _sizeBytesVerificationBlock = sizeBytesVerificationBlock;
        _lastResult = Result.Success;
        _flags = 0;
        _bufferLevel = bufferLevel;
        _isWritable = isWritable;
        _shiftBytesVerificationBlock = (int)BitmapUtils.ILog2((uint)sizeBytesVerificationBlock);

        Assert.SdkEqual(1 << _shiftBytesVerificationBlock, _sizeBytesVerificationBlock);

        SetKeepBurstMode(useKeepBurstMode);
        SetRealDataCache(useRealDataCache);

        return Result.Success;
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public override Result GetSize(out long size)
    {
        throw new NotImplementedException();
    }

    public override Result SetSize(long size)
    {
        throw new NotImplementedException();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        throw new NotImplementedException();
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }

    public override Result Flush()
    {
        throw new NotImplementedException();
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }

    public Result Commit()
    {
        throw new NotImplementedException();
    }

    public Result OnRollback()
    {
        throw new NotImplementedException();
    }

    private Result FillZeroImpl(long offset, long size)
    {
        throw new NotImplementedException();
    }

    private Result DestroySignatureImpl(long offset, long size)
    {
        throw new NotImplementedException();
    }

    private Result InvalidateImpl()
    {
        throw new NotImplementedException();
    }

    private Result QueryRangeImpl(Span<byte> outBuffer, long offset, long size)
    {
        throw new NotImplementedException();
    }

    private Result GetAssociateBuffer(out Buffer outRange, out CacheEntry outEntry, long offset, int idealSize,
        bool isAllocateForWrite)
    {
        throw new NotImplementedException();
    }

    private Result StoreOrDestroyBuffer(in Buffer memoryRange, ref CacheEntry entry)
    {
        throw new NotImplementedException();
    }

    private Result StoreOrDestroyBuffer(out int outCacheIndex, in Buffer memoryRange, ref CacheEntry entry)
    {
        throw new NotImplementedException();
    }

    private Result FlushCacheEntry(int index, bool isWithInvalidate)
    {
        throw new NotImplementedException();
    }

    private Result FlushRangeCacheEntries(long offset, long size, bool isWithInvalidate)
    {
        throw new NotImplementedException();
    }

    private Result FlushAllCacheEntries()
    {
        throw new NotImplementedException();
    }

    private Result ControlDirtiness()
    {
        throw new NotImplementedException();
    }

    private Result UpdateLastResult(Result result)
    {
        throw new NotImplementedException();
    }

    private Result ReadHeadCache(out Buffer outMemoryRange, out CacheEntry outEntry, out bool outIsCacheNeeded,
        ref long offset, ref long offsetAligned, long offsetAlignedEnd, ref Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    private Result ReadTailCache(out Buffer outMemoryRange, out CacheEntry outEntry, out bool outIsCacheNeeded,
        long offset, long offsetAligned, long offsetAlignedEnd, Span<byte> buffer, ref long size)
    {
        throw new NotImplementedException();
    }

    private Result BulkRead(long offset, Span<byte> buffer, ref Buffer memoryRangeHead, ref Buffer memoryRangeTail,
        ref CacheEntry entryHead, ref CacheEntry entryTail, bool isHeadCacheNeeded, bool isTailCacheNeeded)
    {
        throw new NotImplementedException();
    }
}