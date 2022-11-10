using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.FsSystem;

/// <summary>
/// Combines multiple <see cref="IStorage"/>s into a single <see cref="IStorage"/>.
/// </summary>
/// <remarks><para>The <see cref="IndirectStorage"/>'s <see cref="BucketTree"/> contains <see cref="Entry"/>
/// values that describe how the created storage is to be built from the base storages.</para>
/// <para>Based on nnSdk 14.3.0 (FS 14.1.0)</para></remarks>
public class IndirectStorage : IStorage
{
    public static readonly int StorageCount = 2;
    public static readonly int NodeSize = 1024 * 16;

    private BucketTree _table;
    private Array2<ValueSubStorage> _dataStorage;

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Entry
    {
        private long VirtualOffset;
        private long PhysicalOffset;
        public int StorageIndex;

        public void SetVirtualOffset(long offset) => VirtualOffset = offset;
        public readonly long GetVirtualOffset() => VirtualOffset;

        public void SetPhysicalOffset(long offset) => PhysicalOffset = offset;
        public readonly long GetPhysicalOffset() => PhysicalOffset;
    }

    public struct EntryData
    {
        public long VirtualOffset;
        public long PhysicalOffset;
        public int StorageIndex;

        public void Set(in Entry entry)
        {
            VirtualOffset = entry.GetVirtualOffset();
            PhysicalOffset = entry.GetPhysicalOffset();
            StorageIndex = entry.StorageIndex;
        }
    }

    private struct ContinuousReadingEntry : BucketTree.IContinuousReadingEntry
    {
        public int FragmentSizeMax => 1024 * 4;

#pragma warning disable CS0649
        // This field will be read in by BucketTree.Visitor.ScanContinuousReading
        private Entry _entry;
#pragma warning restore CS0649

        public readonly long GetVirtualOffset() => _entry.GetVirtualOffset();
        public readonly long GetPhysicalOffset() => _entry.GetPhysicalOffset();
        public readonly bool IsFragment() => _entry.StorageIndex != 0;
    }

    public IndirectStorage()
    {
        _table = new BucketTree();
    }

    public override void Dispose()
    {
        FinalizeObject();

        Span<ValueSubStorage> items = _dataStorage.Items;
        for (int i = 0; i < items.Length; i++)
            items[i].Dispose();

        _table.Dispose();

        base.Dispose();
    }

    public static long QueryHeaderStorageSize() => BucketTree.QueryHeaderStorageSize();

    public static long QueryNodeStorageSize(int entryCount) =>
        BucketTree.QueryNodeStorageSize(NodeSize, Unsafe.SizeOf<Entry>(), entryCount);

    public static long QueryEntryStorageSize(int entryCount) =>
        BucketTree.QueryEntryStorageSize(NodeSize, Unsafe.SizeOf<Entry>(), entryCount);

    public void SetStorage(int index, in ValueSubStorage storage)
    {
        Assert.SdkRequiresInRange(index, 0, StorageCount);
        _dataStorage[index].Set(in storage);
    }

    public void SetStorage(int index, IStorage storage, long offset, long size)
    {
        Assert.SdkRequiresInRange(index, 0, StorageCount);

        using var subStorage = new ValueSubStorage(storage, offset, size);
        _dataStorage[index].Set(in subStorage);
    }

    protected ref ValueSubStorage GetDataStorage(int index)
    {
        Assert.SdkRequiresInRange(index, 0, StorageCount);
        return ref _dataStorage[index];
    }

    protected BucketTree GetEntryTable()
    {
        return _table;
    }

    public bool IsInitialized()
    {
        return _table.IsInitialized();
    }

    public Result Initialize(MemoryResource allocator, in ValueSubStorage tableStorage)
    {
        Unsafe.SkipInit(out BucketTree.Header header);

        Result res = tableStorage.Read(0, SpanHelpers.AsByteSpan(ref header));
        if (res.IsFailure()) return res.Miss();

        res = header.Verify();
        if (res.IsFailure()) return res.Miss();

        long nodeStorageSize = QueryNodeStorageSize(header.EntryCount);
        long entryStorageSize = QueryEntryStorageSize(header.EntryCount);
        long nodeStorageOffset = QueryHeaderStorageSize();
        long entryStorageOffset = nodeStorageSize + nodeStorageOffset;

        res = tableStorage.GetSize(out long storageSize);
        if (res.IsFailure()) return res.Miss();

        if (storageSize < entryStorageOffset + entryStorageSize)
            return ResultFs.InvalidIndirectStorageBucketTreeSize.Log();

        using var nodeStorage = new ValueSubStorage(tableStorage, nodeStorageOffset, nodeStorageSize);
        using var entryStorage = new ValueSubStorage(tableStorage, entryStorageOffset, entryStorageSize);

        return Initialize(allocator, in nodeStorage, in entryStorage, header.EntryCount);
    }

    public Result Initialize(MemoryResource allocator, in ValueSubStorage nodeStorage, in ValueSubStorage entryStorage,
        int entryCount)
    {
        return _table.Initialize(allocator, in nodeStorage, in entryStorage, NodeSize, Unsafe.SizeOf<Entry>(),
            entryCount);
    }

    public void FinalizeObject()
    {
        if (IsInitialized())
        {
            _table.FinalizeObject();

            Span<ValueSubStorage> storages = _dataStorage.Items;
            for (int i = 0; i < storages.Length; i++)
            {
                using var emptySubStorage = new ValueSubStorage();
                storages[i].Set(in emptySubStorage);
            }
        }
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        // Validate pre-conditions
        Assert.SdkRequiresLessEqual(0, offset);
        Assert.SdkRequires(IsInitialized());

        // Succeed if there's nothing to read
        if (destination.Length == 0)
            return Result.Success;

        var closure = new OperatePerEntryClosure { OutBuffer = destination, Offset = offset };

        Result res = OperatePerEntry(offset, destination.Length, enableContinuousReading: true, verifyEntryRanges: true, ref closure,
            static (ref ValueSubStorage storage, long physicalOffset, long virtualOffset, long size, ref OperatePerEntryClosure entryClosure) =>
            {
                int bufferPosition = (int)(virtualOffset - entryClosure.Offset);
                Result res = storage.Read(physicalOffset, entryClosure.OutBuffer.Slice(bufferPosition, (int)size));
                if (res.IsFailure()) return res.Miss();

                return Result.Success;
            });

        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        return ResultFs.UnsupportedWriteForIndirectStorage.Log();
    }

    public override Result Flush()
    {
        return Result.Success;
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        Result res = _table.GetOffsets(out BucketTree.Offsets offsets);
        if (res.IsFailure()) return res.Miss();

        size = offsets.EndOffset;
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForIndirectStorage.Log();
    }

    public Result GetEntryList(Span<Entry> entryBuffer, out int outputEntryCount, long offset, long size)
    {
        UnsafeHelpers.SkipParamInit(out outputEntryCount);

        // Validate pre-conditions
        Assert.SdkRequiresLessEqual(0, offset);
        Assert.SdkRequiresLessEqual(0, size);
        Assert.SdkRequires(IsInitialized());

        // Succeed if there's no range
        if (size == 0)
        {
            outputEntryCount = 0;
            return Result.Success;
        }

        // Check that our range is valid
        Result res = _table.GetOffsets(out BucketTree.Offsets offsets);
        if (res.IsFailure()) return res.Miss();

        if (!offsets.IsInclude(offset, size))
            return ResultFs.OutOfRange.Log();


        // Find the offset in our tree
        using var visitor = new BucketTree.Visitor();

        res = _table.Find(ref visitor.Ref, offset);
        if (res.IsFailure()) return res.Miss();

        long entryOffset = visitor.Get<Entry>().GetVirtualOffset();
        if (entryOffset < 0 || !offsets.IsInclude(entryOffset))
            return ResultFs.InvalidIndirectEntryOffset.Log();

        // Prepare to loop over entries
        long endOffset = offset + size;
        int count = 0;

        var currentEntry = visitor.Get<Entry>();
        while (currentEntry.GetVirtualOffset() < endOffset)
        {
            // Try to write the entry to the out list
            if (entryBuffer.Length != 0)
            {
                if (count >= entryBuffer.Length)
                    break;

                entryBuffer[count] = currentEntry;
            }

            count++;

            // Advance
            if (!visitor.CanMoveNext())
                break;

            res = visitor.MoveNext();
            if (res.IsFailure()) return res.Miss();

            currentEntry = visitor.Get<Entry>();
        }

        outputEntryCount = count;
        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        Assert.SdkRequiresLessEqual(0, offset);
        Assert.SdkRequiresLessEqual(0, size);
        Assert.SdkRequires(IsInitialized());

        switch (operationId)
        {
            case OperationId.InvalidateCache:
                if (!_table.IsEmpty())
                {
                    Result res = _table.InvalidateCache();
                    if (res.IsFailure()) return res.Miss();

                    for (int i = 0; i < _dataStorage.Items.Length; i++)
                    {
                        res = _dataStorage.Items[i].OperateRange(OperationId.InvalidateCache, 0, long.MaxValue);
                        if (res.IsFailure()) return res.Miss();
                    }
                }
                break;
            case OperationId.QueryRange:
                if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                    return ResultFs.InvalidArgument.Log();

                if (size > 0)
                {
                    Result res = _table.GetOffsets(out BucketTree.Offsets offsets);
                    if (res.IsFailure()) return res.Miss();

                    if (!offsets.IsInclude(offset, size))
                        return ResultFs.OutOfRange.Log();

                    if (!_table.IsEmpty())
                    {
                        var closure = new OperatePerEntryClosure { OperationId = operationId, InBuffer = inBuffer };

                        res = OperatePerEntry(offset, size, enableContinuousReading: false, verifyEntryRanges: true, ref closure,
                            static (ref ValueSubStorage storage, long physicalOffset, long virtualOffset, long processSize, ref OperatePerEntryClosure closure) =>
                            {
                                Unsafe.SkipInit(out QueryRangeInfo currentInfo);
                                Result res = storage.OperateRange(SpanHelpers.AsByteSpan(ref currentInfo),
                                    closure.OperationId, physicalOffset, processSize, closure.InBuffer);
                                if (res.IsFailure()) return res.Miss();

                                closure.InfoMerged.Merge(in currentInfo);
                                return Result.Success;
                            });
                        if (res.IsFailure()) return res.Miss();

                        SpanHelpers.AsByteSpan(ref closure.InfoMerged).CopyTo(outBuffer);
                    }
                }
                break;
            default:
                return ResultFs.UnsupportedOperateRangeForIndirectStorage.Log();
        }

        return Result.Success;
    }

    protected delegate Result OperatePerEntryFunc(ref ValueSubStorage storage, long physicalOffset, long virtualOffset,
        long processSize, ref OperatePerEntryClosure closure);

    protected ref struct OperatePerEntryClosure
    {
        public Span<byte> OutBuffer;
        public ReadOnlySpan<byte> InBuffer;
        public long Offset;
        public OperationId OperationId;
        public QueryRangeInfo InfoMerged;
    }

    protected Result OperatePerEntry(long offset, long size, bool enableContinuousReading, bool verifyEntryRanges,
        ref OperatePerEntryClosure closure, OperatePerEntryFunc func)
    {
        // Validate preconditions
        Assert.SdkRequiresLessEqual(0, offset);
        Assert.SdkRequiresLessEqual(0, size);
        Assert.SdkRequires(IsInitialized());

        // Succeed if there's nothing to operate on
        if (size == 0)
            return Result.Success;

        // Validate arguments
        Result res = _table.GetOffsets(out BucketTree.Offsets offsets);
        if (res.IsFailure()) return res.Miss();

        if (!offsets.IsInclude(offset, size))
            return ResultFs.OutOfRange.Log();

        // Find the offset in our tree
        using var visitor = new BucketTree.Visitor();

        res = _table.Find(ref visitor.Ref, offset);
        if (res.IsFailure()) return res.Miss();

        long entryOffset = visitor.Get<Entry>().GetVirtualOffset();
        if (entryOffset < 0 || !offsets.IsInclude(entryOffset))
            return ResultFs.InvalidIndirectEntryOffset.Log();

        // Prepare to operate in chunks
        long currentOffset = offset;
        long endOffset = offset + size;
        var continuousReading = new BucketTree.ContinuousReadingInfo();

        while (currentOffset < endOffset)
        {
            // Get the current entry
            var currentEntry = visitor.Get<Entry>();

            // Get and validate the entry's offset
            long currentEntryOffset = currentEntry.GetVirtualOffset();
            if (currentEntryOffset > currentOffset)
                return ResultFs.InvalidIndirectEntryOffset.Log();

            // Validate the storage index
            if (currentEntry.StorageIndex < 0 || currentEntry.StorageIndex >= StorageCount)
                return ResultFs.InvalidIndirectEntryStorageIndex.Log();

            if (enableContinuousReading)
            {
                if (continuousReading.CheckNeedScan())
                {
                    res = visitor.ScanContinuousReading<ContinuousReadingEntry>(out continuousReading, currentOffset,
                        endOffset - currentOffset);
                    if (res.IsFailure()) return res.Miss();
                }

                if (continuousReading.CanDo())
                {
                    if (currentEntry.StorageIndex != 0)
                        return ResultFs.InvalidIndirectStorageIndex.Log();

                    long offsetInEntry = currentOffset - currentEntryOffset;
                    long entryStorageOffset = currentEntry.GetPhysicalOffset();
                    long dataStorageOffset = entryStorageOffset + offsetInEntry;

                    long continuousReadSize = continuousReading.GetReadSize();

                    if (verifyEntryRanges)
                    {
                        res = _dataStorage[0].GetSize(out long storageSize);
                        if (res.IsFailure()) return res.Miss();

                        // Ensure that we remain within range
                        if (entryStorageOffset < 0 || entryStorageOffset > storageSize)
                            return ResultFs.InvalidIndirectEntryOffset.Log();

                        if (dataStorageOffset + continuousReadSize > storageSize)
                            return ResultFs.InvalidIndirectStorageSize.Log();
                    }

                    res = func(ref _dataStorage[0], dataStorageOffset, currentOffset, continuousReadSize, ref closure);
                    if (res.IsFailure()) return res.Miss();

                    continuousReading.Done();
                }
            }

            // Get and validate the next entry offset
            long nextEntryOffset;
            if (visitor.CanMoveNext())
            {
                res = visitor.MoveNext();
                if (res.IsFailure()) return res.Miss();

                nextEntryOffset = visitor.Get<Entry>().GetVirtualOffset();
                if (!offsets.IsInclude(nextEntryOffset))
                    return ResultFs.InvalidIndirectEntryOffset.Log();
            }
            else
            {
                nextEntryOffset = offsets.EndOffset;
            }

            if (currentOffset >= nextEntryOffset)
                return ResultFs.InvalidIndirectEntryOffset.Log();

            // Get the offset of the data we need in the entry 
            long dataOffsetInEntry = currentOffset - currentEntryOffset;
            long dataSize = nextEntryOffset - currentEntryOffset - dataOffsetInEntry;
            Assert.SdkLess(0, dataSize);

            // Determine how much is left
            long remainingSize = endOffset - currentOffset;
            long processSize = Math.Min(remainingSize, dataSize);
            Assert.SdkLessEqual(processSize, size);

            // Operate, if we need to
            bool needsOperate;
            if (!enableContinuousReading)
            {
                needsOperate = true;
            }
            else
            {
                needsOperate = !continuousReading.IsDone() || currentEntry.StorageIndex != 0;
            }

            if (needsOperate)
            {
                long entryStorageOffset = currentEntry.GetPhysicalOffset();
                long dataStorageOffset = entryStorageOffset + dataOffsetInEntry;

                if (verifyEntryRanges)
                {
                    res = _dataStorage[currentEntry.StorageIndex].GetSize(out long storageSize);
                    if (res.IsFailure()) return res.Miss();

                    // Ensure that we remain within range
                    if (entryStorageOffset < 0 || entryStorageOffset > storageSize)
                        return ResultFs.IndirectStorageCorrupted.Log();

                    if (dataStorageOffset + processSize > storageSize)
                        return ResultFs.IndirectStorageCorrupted.Log();
                }

                res = func(ref _dataStorage[currentEntry.StorageIndex], dataStorageOffset, currentOffset, processSize,
                    ref closure);
                if (res.IsFailure()) return res.Miss();
            }

            currentOffset += processSize;
        }

        return Result.Success;
    }
}