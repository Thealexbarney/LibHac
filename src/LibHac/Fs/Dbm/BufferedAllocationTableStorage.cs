// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using System.Collections.Generic;
using LibHac.Common.FixedArrays;
using Buffer = LibHac.Mem.Buffer;

namespace LibHac.Fs.Dbm;

file static class Anonymous
{
    public static bool Overlaps(long offset1, long size1, long offset2, long size2)
    {
        return offset1 < offset2 + size2 && offset2 < offset1 + size1;
    }
}

public class BufferedAllocationTableStorage : AllocationTableStorage
{
    public struct CacheEntry
    {
        public Buffer Buffer;
        public long Offset;
        public long Handle;
    }

    public struct CacheEntriesNode
    {
        public (ulong, ulong) MemoryRange;
        public Array16<CacheEntry> Entries;
    }

    private LinkedList<CacheEntriesNode> _cacheEntriesList;
    private int _enableCount;
    private IBufferManager _bufferManager;

    public BufferedAllocationTableStorage()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result InitializeBuffered(AllocationTable allocationTable, uint index, long blockSize,
        in ValueSubStorage dataStorage, IBufferManager bufferManager)
    {
        throw new NotImplementedException();
    }

    public override void FinalizeObject()
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

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }

    private void UpdateCache(long offset, ReadOnlySpan<byte> buffer)
    {
        throw new NotImplementedException();
    }

    private void InvalidateCache(long offset, long size)
    {
        throw new NotImplementedException();
    }

    private void DeallocateCache(ref CacheEntry cache)
    {
        throw new NotImplementedException();
    }

    private void AcquireCacheBuffer(out CacheEntry outCache)
    {
        throw new NotImplementedException();
    }

    private void ExtendCacheLife(LinkedListNode<CacheEntriesNode> iterator, int index)
    {
        throw new NotImplementedException();
    }

    public void EnterHoldingCacheSection()
    {
        throw new NotImplementedException();
    }

    public void LeaveHoldingCacheSection()
    {
        throw new NotImplementedException();
    }

    private LinkedListNode<CacheEntriesNode> AllocateCacheEntries()
    {
        throw new NotImplementedException();
    }
}