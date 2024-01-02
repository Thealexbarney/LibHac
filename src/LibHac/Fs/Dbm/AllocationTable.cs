// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;

namespace LibHac.Fs.Dbm;

public class AllocationTable : IDisposable
{
    private ValueSubStorage _storage;
    private uint _blockCount;

    private struct TableElement
    {
        public uint Prev;
        public uint Next;

        public readonly bool IsChainChild() => throw new NotImplementedException();

        public readonly bool IsChainParent() => throw new NotImplementedException();

        public readonly bool IsFreeListEmpty() => throw new NotImplementedException();

        public readonly bool IsListHead() => throw new NotImplementedException();

        public readonly bool IsSingleParent() => throw new NotImplementedException();

        public readonly bool IsTailParent() => throw new NotImplementedException();

        public void SetChainChild(uint startIndex, uint chainLength)
        {
            throw new NotImplementedException();
        }

        public void SetChainParent(uint prevSector, uint nextSector, bool isChainParent)
        {
            throw new NotImplementedException();
        }

        public void SetListFreeHeader()
        {
            throw new NotImplementedException();
        }

        public void SetListHead(uint nextSectorIndex, bool isChainParent)
        {
            throw new NotImplementedException();
        }
    }

    public AllocationTable()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public static long QuerySize(uint blockCount)
    {
        throw new NotImplementedException();
    }

    public static Result Format(in ValueSubStorage storage, uint blockCount)
    {
        throw new NotImplementedException();
    }

    public static Result Expand(in ValueSubStorage storage, uint oldBlockCount, uint newBlockCount)
    {
        throw new NotImplementedException();
    }

    public void Initialize(in ValueSubStorage storage, uint blockCount)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public uint GetBlockCount()
    {
        throw new NotImplementedException();
    }

    public Result Invalidate()
    {
        throw new NotImplementedException();
    }

    public Result Allocate(out uint outIndex, uint blockCount)
    {
        throw new NotImplementedException();
    }

    public Result Free(uint index)
    {
        throw new NotImplementedException();
    }

    public Result ReadNext(out uint outNextIndex, out uint outBlockCount, uint index)
    {
        throw new NotImplementedException();
    }

    public Result ReadPrevious(out uint outPrevIndex, out uint outBlockCount, uint index)
    {
        throw new NotImplementedException();
    }

    private uint GetReadBlockCount(uint sectorIndex)
    {
        throw new NotImplementedException();
    }

    public Result CalcTotalBlockCount(out uint outBlockCount, uint startIndex)
    {
        throw new NotImplementedException();
    }

    public Result LookupTailParentCount(out uint outTailIndex, uint index)
    {
        throw new NotImplementedException();
    }

    public Result Concat(uint list1Index, uint list2Index)
    {
        throw new NotImplementedException();
    }

    public Result Split(out uint outIndexAfter, uint index, uint count)
    {
        throw new NotImplementedException();
    }

    public Result ReadFreeListHead(out uint outIndex)
    {
        throw new NotImplementedException();
    }

    public Result CalcFreeListLength(out uint outCount)
    {
        throw new NotImplementedException();
    }

    private uint ConvertIndexToSector(uint index)
    {
        throw new NotImplementedException();
    }

    private uint ConvertSectorToIndex(uint sector)
    {
        throw new NotImplementedException();
    }

    private Result ReadBlock(out TableElement outDst, uint sectorIndex, uint blockCount)
    {
        throw new NotImplementedException();
    }

    private Result WriteBlock(in TableElement source, uint sectorIndex, uint nextSectorIndex)
    {
        throw new NotImplementedException();
    }

    private Result UpdatePrevious(in TableElement source, uint sectorIndex, uint previousSectorIndex)
    {
        throw new NotImplementedException();
    }

    private Result UpdateChain(uint headSector, uint parentSector, uint lastSector, uint prevSector, uint nextSector)
    {
        throw new NotImplementedException();
    }

    private Result ReadFreeList(out TableElement outFreeList)
    {
        throw new NotImplementedException();
    }

    private Result AllocateFreeSector(out uint outSector, out uint outBlockCount, ref TableElement freeListHeader,
        uint blockCount)
    {
        throw new NotImplementedException();
    }

    private Result UpdateFreeList(in TableElement freeList)
    {
        throw new NotImplementedException();
    }
}