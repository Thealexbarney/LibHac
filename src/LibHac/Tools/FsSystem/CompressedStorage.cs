using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Util;

namespace LibHac.Tools.FsSystem;

internal class CompressedStorage : IStorage
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Entry
    {
        public long VirtualOffset;
        public long PhysicalOffset;
        public CompressionType CompressionType;
        public sbyte CompressionLevel;
        public uint PhysicalSize;
    }

    public static readonly int NodeSize = 0x4000;

    public static long QueryEntryStorageSize(int entryCount)
    {
        return BucketTree.QueryEntryStorageSize(NodeSize, Unsafe.SizeOf<Entry>(), entryCount);
    }

    public static long QueryNodeStorageSize(int entryCount)
    {
        return BucketTree.QueryNodeStorageSize(NodeSize, Unsafe.SizeOf<Entry>(), entryCount);
    }

    private readonly BucketTree _bucketTree;
    private ValueSubStorage _dataStorage;

    public CompressedStorage()
    {
        _bucketTree = new BucketTree();
        _dataStorage = new ValueSubStorage();
    }

    public Result Initialize(MemoryResource allocatorForBucketTree, in ValueSubStorage dataStorage,
        in ValueSubStorage nodeStorage, in ValueSubStorage entryStorage, int bucketTreeEntryCount)
    {
        nodeStorage.GetSubStorage().WriteAllBytes("nodeStorage");
        entryStorage.GetSubStorage().WriteAllBytes("entryStorage");

        Result rc = _bucketTree.Initialize(allocatorForBucketTree, in nodeStorage, in entryStorage, NodeSize,
            Unsafe.SizeOf<Entry>(), bucketTreeEntryCount);
        if (rc.IsFailure()) return rc.Miss();

        _dataStorage.Set(in dataStorage);

        return Result.Success;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        // Validate arguments
        Result rc = _bucketTree.GetOffsets(out BucketTree.Offsets offsets);
        if (rc.IsFailure()) return rc.Miss();

        if (!offsets.IsInclude(offset, destination.Length))
            return ResultFs.OutOfRange.Log();

        // Find the offset in our tree
        using var visitor = new BucketTree.Visitor();

        rc = _bucketTree.Find(ref visitor.Ref, offset);
        if (rc.IsFailure()) return rc;

        long entryOffset = visitor.Get<Entry>().VirtualOffset;
        if (entryOffset < 0 || !offsets.IsInclude(entryOffset))
            return ResultFs.UnexpectedInCompressedStorageA.Log();

        // Prepare to operate in chunks
        long currentOffset = offset;
        long endOffset = offset + destination.Length;

        byte[] workBufferEnc = null;
        byte[] workBufferDec = null;

        while (currentOffset < endOffset)
        {
            // Get the current entry
            var currentEntry = visitor.Get<Entry>();

            // Get and validate the entry's offset
            long currentEntryOffset = currentEntry.VirtualOffset;
            if (currentEntryOffset > currentOffset)
                return ResultFs.UnexpectedInCompressedStorageA.Log();

            // Get and validate the next entry offset
            long nextEntryOffset;
            if (visitor.CanMoveNext())
            {
                rc = visitor.MoveNext();
                if (rc.IsFailure()) return rc;

                nextEntryOffset = visitor.Get<Entry>().VirtualOffset;
                if (!offsets.IsInclude(nextEntryOffset))
                    return ResultFs.UnexpectedInCompressedStorageA.Log();
            }
            else
            {
                nextEntryOffset = offsets.EndOffset;
            }

            if (currentOffset >= nextEntryOffset)
                return ResultFs.UnexpectedInCompressedStorageA.Log();

            // Get the offset of the data we need in the entry 
            long dataOffsetInEntry = currentOffset - currentEntryOffset;
            long currentEntrySize = nextEntryOffset - currentEntryOffset;

            // Determine how much is left
            long remainingSize = endOffset - currentOffset;
            long toWriteSize = Math.Min(remainingSize, currentEntrySize - dataOffsetInEntry);
            Assert.SdkLessEqual(toWriteSize, destination.Length);

            Span<byte> entryDestination = destination.Slice((int)(currentOffset - offset), (int)toWriteSize);

            if (currentEntry.CompressionType == CompressionType.Lz4)
            {
                EnsureBufferSize(ref workBufferEnc, (int)currentEntry.PhysicalSize);
                EnsureBufferSize(ref workBufferDec, (int)currentEntrySize);

                Span<byte> encBuffer = workBufferEnc.AsSpan(0, (int)currentEntry.PhysicalSize);
                Span<byte> decBuffer = workBufferDec.AsSpan(0, (int)currentEntrySize);

                rc = _dataStorage.Read(currentEntry.PhysicalOffset, encBuffer);
                if (rc.IsFailure()) return rc.Miss();

                Lz4.Decompress(encBuffer, decBuffer);

                decBuffer.Slice((int)dataOffsetInEntry, (int)toWriteSize).CopyTo(entryDestination);
            }
            else if (currentEntry.CompressionType == CompressionType.None)
            {
                rc = _dataStorage.Read(currentEntry.PhysicalOffset + dataOffsetInEntry, entryDestination);
                if (rc.IsFailure()) return rc.Miss();
            }
            else if (currentEntry.CompressionType == CompressionType.Zeroed)
            {
                entryDestination.Clear();
            }

            currentOffset += toWriteSize;
        }

        if (workBufferDec is not null)
            ArrayPool<byte>.Shared.Return(workBufferDec);

        if (workBufferEnc is not null)
            ArrayPool<byte>.Shared.Return(workBufferEnc);

        return Result.Success;

        static void EnsureBufferSize(ref byte[] buffer, int requiredSize)
        {
            if (buffer is null || buffer.Length < requiredSize)
            {
                if (buffer is not null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                buffer = ArrayPool<byte>.Shared.Rent(requiredSize);
            }

            Assert.SdkGreaterEqual(buffer.Length, requiredSize);
        }
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
        UnsafeHelpers.SkipParamInit(out size);

        Result rc = _bucketTree.GetOffsets(out BucketTree.Offsets offsets);
        if (rc.IsFailure()) return rc.Miss();

        size = offsets.EndOffset;
        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}