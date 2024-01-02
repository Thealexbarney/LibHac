// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using System.Runtime.InteropServices;
using LibHac.Common.FixedArrays;
using LibHac.Fs;

namespace LibHac.FsSystem;

[StructLayout(LayoutKind.Sequential)]
public struct MappingTableControlArea
{
    public uint Version;
    public uint AvailableBlockCount;
    public uint ReservedBlockCount;
    public Array4<byte> Reserved;
}

public class MappingTable : IDisposable
{
    private struct MappingEntry
    {
        public uint PhysicalIndex;
        public uint VirtualIndex;
    }

    public struct Iterator
    {
        public uint VirtualIndex;
        public uint PhysicalIndex;
        public uint CountContinue;
        public uint CountRemain;

        public Iterator()
        {
            throw new NotImplementedException();
        }

        public readonly uint GetBlockCount()
        {
            throw new NotImplementedException();
        }

        public readonly uint GetPhysicalIndex()
        {
            throw new NotImplementedException();
        }
    }

    private uint _version;
    private uint _mappingEntryCount;
    private uint _journalBlockCount;

    private ValueSubStorage _tableStorage;
    private ValueSubStorage _bitmapUpdatedPhysicalStorage;
    private ValueSubStorage _bitmapUpdatedVirtualStorage;
    private ValueSubStorage _bitmapUnassignedStorage;

    private Bitmap _bitmapUpdatedPhysical;
    private Bitmap _bitmapUpdatedVirtual;
    private Bitmap _bitmapUnassigned;

    public MappingTable()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private uint GetTotalBlockCount()
    {
        throw new NotImplementedException();
    }

    private static uint GetValidIndex(uint index)
    {
        throw new NotImplementedException();
    }

    private static bool IsPhysicalIndex(uint index)
    {
        throw new NotImplementedException();
    }

    private static uint PhysicalIndex(uint virtualIndex)
    {
        throw new NotImplementedException();
    }

    public static uint MakeIndex(long value)
    {
        throw new NotImplementedException();
    }

    public static void QueryMappingMetaSize(out long outTableSize, out long outBitmapSizeUpdatedPhysical,
        out long outBitmapSizeUpdatedVirtual, out long outBitmapSizeUnassigned, uint availableBlocks,
        uint journalBlocks)
    {
        throw new NotImplementedException();
    }

    public static Result Format(SubStorage storageMappingControlArea, SubStorage storageTable,
        SubStorage storageBitmapPhysical, SubStorage storageBitmapVirtual, SubStorage storageBitmapUnassigned,
        uint blockCountAvailableArea, uint blockCountReservedArea)
    {
        throw new NotImplementedException();
    }

    public static Result Expand(SubStorage storageMappingControlArea, SubStorage storageTable,
        SubStorage storageBitmapPhysical, SubStorage storageBitmapVirtual, SubStorage storageBitmapUnassigned,
        uint blockCountAreaNew, uint blockCountReservedAreaNew)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(SubStorage storageMappingControlArea, SubStorage storageTable,
        SubStorage storageBitmapPhysical, SubStorage storageBitmapVirtual, SubStorage storageBitmapUnassigned)
    {
        throw new NotImplementedException();
    }

    private Result CheckPhysicalIndex(uint index)
    {
        throw new NotImplementedException();
    }

    private Result CheckVirtualIndex(uint index)
    {
        throw new NotImplementedException();
    }

    private Result ReadMappingEntry(out MappingEntry outValue, uint index)
    {
        throw new NotImplementedException();
    }

    private Result ReadMappingEntries(Span<MappingEntry> buffer, uint index, int count)
    {
        throw new NotImplementedException();
    }

    private Result WriteMappingEntry(in MappingEntry entry, uint index)
    {
        throw new NotImplementedException();
    }

    private Result WriteMappingEntries(ReadOnlySpan<MappingEntry> entries, uint index)
    {
        throw new NotImplementedException();
    }

    public Result GetPhysicalIndex(out uint outValue, uint index)
    {
        throw new NotImplementedException();
    }

    private Result MarkAssignment(out uint outIndexDst, out uint outCountAssign, uint index, uint count)
    {
        throw new NotImplementedException();
    }

    private Result MarkUpdateRecursively(out bool outNeedCopyHead, out uint outHeadSrc, out uint outHeadDst,
        out bool outNeedCopyTail, out uint outTailSrc, out uint outTailDst, uint index, uint count)
    {
        throw new NotImplementedException();
    }

    public Result MarkUpdate(out bool outNeedCopyHead, out uint outHeadSrc, out uint outHeadDst,
        out bool outNeedCopyTail, out uint outTailSrc, out uint outTailDst, uint index, uint count)
    {
        throw new NotImplementedException();
    }

    public Result MakeIterator(out Iterator outIterator, uint index, uint count)
    {
        throw new NotImplementedException();
    }

    public Result UpdateIterator(ref Iterator it)
    {
        throw new NotImplementedException();
    }

    public Result InvalidateCache()
    {
        throw new NotImplementedException();
    }

    private Result UpdateMapTable(uint physicalIndex, uint virtualIndex, uint countContinue)
    {
        throw new NotImplementedException();
    }

    private Result AddUnassignedListIteratively(bool useVirtualIndex)
    {
        throw new NotImplementedException();
    }

    private Result AddUnassignedList(uint index, uint count, bool useVirtualIndex)
    {
        throw new NotImplementedException();
    }

    public Result Commit()
    {
        throw new NotImplementedException();
    }

    public Result Rollback()
    {
        throw new NotImplementedException();
    }

    private Result GetUnassignedCount(out uint outCount)
    {
        throw new NotImplementedException();
    }
}