// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
using System;
using LibHac.Common.FixedArrays;
using LibHac.Fs;

namespace LibHac.FsSystem;

internal struct JournalStorageControlArea
{
    public uint Magic;
    public uint Version;
    public long TotalSize;
    public long JournalSize;
    public long SizeBlock;
    public MappingTableControlArea MappingTableControlArea;
    public Array464<byte> Reserved;
}

public class JournalStorage : IStorage
{
    private JournalStorageControlArea _controlArea;
    private MappingTable _mappingTable;
    private ValueSubStorage _dataStorage;
    private bool _isMappingTableFrozen;

    public JournalStorage()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public static void QueryMappingMetaSize(out long outTableSize, out long outBitmapSizeUpdatedPhysical,
        out long outBitmapSizeUpdatedVirtual, out long outBitmapSizeUnassigned, long sizeArea, long sizeReserved,
        long sizeBlock)
    {
        throw new NotImplementedException();
    }

    public static Result Format(
        in ValueSubStorage storageJournalControlArea,
        in ValueSubStorage storageJournalTable,
        in ValueSubStorage storageBitmapPhysical,
        in ValueSubStorage storageBitmapVirtual,
        in ValueSubStorage storageBitmapUnassigned,
        long sizeArea,
        long sizeReservedArea,
        long sizeBlock)
    {
        throw new NotImplementedException();
    }

    public static Result Expand(
        in ValueSubStorage storageJournalControlArea,
        in ValueSubStorage storageJournalTable,
        in ValueSubStorage storageBitmapPhysical,
        in ValueSubStorage storageBitmapVirtual,
        in ValueSubStorage storageBitmapUnassigned,
        long sizeAreaNew,
        long sizeReservedAreaNew)
    {
        throw new NotImplementedException();
    }

    public long GetBlockSize()
    {
        throw new NotImplementedException();
    }

    public long GetDataAreaSize()
    {
        throw new NotImplementedException();
    }

    public long GetReservedAreaSize()
    {
        throw new NotImplementedException();
    }

    public ValueSubStorage CreateDataStorage()
    {
        throw new NotImplementedException();
    }

    public bool IsMappingTableFrozen()
    {
        throw new NotImplementedException();
    }

    public void SetMappingTableFrozen(bool isFrozen)
    {
        throw new NotImplementedException();
    }

    private long AlignAddress(long address)
    {
        throw new NotImplementedException();
    }

    private long AlignDown64(long address, long blockSize)
    {
        throw new NotImplementedException();
    }

    private long AlignUpAddress(long address)
    {
        throw new NotImplementedException();
    }

    private long AlignUp64(long address, long blockSize)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(
        in ValueSubStorage storageJournalControlArea,
        in ValueSubStorage storageJournalTable,
        in ValueSubStorage storageJournalBitmapPhysical,
        in ValueSubStorage storageJournalBitmapVirtual,
        in ValueSubStorage storageJournalBitmapUnassigned,
        in ValueSubStorage storageData)
    {
        throw new NotImplementedException();
    }

    private Result GetPhysicalAddress(out long outValue, uint index)
    {
        throw new NotImplementedException();
    }

    private Result MarkUpdate(long offset, long size)
    {
        throw new NotImplementedException();
    }

    public Result Commit()
    {
        throw new NotImplementedException();
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        throw new NotImplementedException();
    }

    public override Result Flush()
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

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }

    private delegate Result IterateMappingTableFunc(long offset, long size, ref IterateMappingTableClosure closure);

    private ref struct IterateMappingTableClosure
    {
        public ReadOnlySpan<byte> InBuffer;
        public Span<byte> OutBuffer;
        public JournalStorage JournalStorage;
        public OperationId OperationId;
    }

    private Result IterateMappingTable(long offset, long size, IterateMappingTableFunc func,
        ref IterateMappingTableClosure closure)
    {
        throw new NotImplementedException();
    }
}