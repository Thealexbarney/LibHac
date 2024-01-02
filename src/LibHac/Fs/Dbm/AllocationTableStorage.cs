// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;

namespace LibHac.Fs.Dbm;

public class AllocationTableStorage : IStorage
{
    private const int MinBlockSize = 2;

    private AllocationTable _allocationTable;
    private ValueSubStorage _dataStorage;
    private int _startIndex;
    private uint _blockSizeShift;
    private int _blockOffset;
    private int _index;
    private uint _blockCount;
    private uint _nextIndex;

    public AllocationTableStorage()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    protected uint GetBlockSizeShift()
    {
        throw new NotImplementedException();
    }

    public long GetBlockSize()
    {
        throw new NotImplementedException();
    }

    public long GetBlockSize(uint blockCount)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(AllocationTable allocationTable, uint blockIndex, long blockSize, in ValueSubStorage dataStorage)
    {
        throw new NotImplementedException();
    }

    public virtual void FinalizeObject()
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

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }

    public Result AllocateBlock(uint blockCount)
    {
        throw new NotImplementedException();
    }

    protected Result SeekTo(out uint outIndex, out uint outBlockCount, out long outOffset, long offset)
    {
        throw new NotImplementedException();
    }

    protected Result SeekToTop()
    {
        throw new NotImplementedException();
    }
}