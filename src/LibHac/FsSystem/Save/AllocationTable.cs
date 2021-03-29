using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using LibHac.Fs;

namespace LibHac.FsSystem.Save
{
    public class AllocationTable
    {
        private const int FreeListEntryIndex = 0;
        private const int EntrySize = 8;

        private IStorage BaseStorage { get; }
        private IStorage HeaderStorage { get; }

        public AllocationTableHeader Header { get; }

        public IStorage GetBaseStorage() => BaseStorage;
        public IStorage GetHeaderStorage() => HeaderStorage;

        public AllocationTable(IStorage storage, IStorage header)
        {
            BaseStorage = storage;
            HeaderStorage = header;
            Header = new AllocationTableHeader(HeaderStorage);
        }

        public void ReadEntry(int blockIndex, out int next, out int previous, out int length)
        {
            int entryIndex = BlockToEntryIndex(blockIndex);

            Span<AllocationTableEntry> entries = stackalloc AllocationTableEntry[2];
            ReadEntries(entryIndex, entries);

            if (entries[0].IsSingleBlockSegment())
            {
                length = 1;

                if (entries[0].IsRangeEntry())
                {
                    ThrowHelper.ThrowResult(ResultFs.AllocationTableIteratedRangeEntry.Value);
                }
            }
            else
            {
                length = entries[1].Next - entryIndex + 1;
            }

            if (entries[0].IsListEnd())
            {
                next = -1;
            }
            else
            {
                next = EntryIndexToBlock(entries[0].GetNext());
            }

            if (entries[0].IsListStart())
            {
                previous = -1;
            }
            else
            {
                previous = EntryIndexToBlock(entries[0].GetPrev());
            }
        }

        public int GetFreeListBlockIndex()
        {
            return EntryIndexToBlock(GetFreeListEntryIndex());
        }

        public void SetFreeListBlockIndex(int headBlockIndex)
        {
            SetFreeListEntryIndex(BlockToEntryIndex(headBlockIndex));
        }

        public int GetFreeListEntryIndex()
        {
            AllocationTableEntry freeList = ReadEntry(FreeListEntryIndex);
            return freeList.GetNext();
        }

        public void SetFreeListEntryIndex(int headBlockIndex)
        {
            var freeList = new AllocationTableEntry { Next = headBlockIndex };
            WriteEntry(FreeListEntryIndex, freeList);
        }

        public int Allocate(int blockCount)
        {
            if (blockCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blockCount));
            }

            int freeList = GetFreeListBlockIndex();

            int newFreeList = Trim(freeList, blockCount);
            if (newFreeList == -1) return -1;

            SetFreeListBlockIndex(newFreeList);

            return freeList;
        }

        public void Free(int listBlockIndex)
        {
            int listEntryIndex = BlockToEntryIndex(listBlockIndex);
            AllocationTableEntry listEntry = ReadEntry(listEntryIndex);

            if (!listEntry.IsListStart())
            {
                throw new ArgumentOutOfRangeException(nameof(listBlockIndex), "The block to free must be the start of a list.");
            }

            int freeListIndex = GetFreeListEntryIndex();

            // Free list is empty
            if (freeListIndex == 0)
            {
                SetFreeListEntryIndex(listEntryIndex);
                return;
            }

            Join(listBlockIndex, EntryIndexToBlock(freeListIndex));

            SetFreeListBlockIndex(listBlockIndex);
        }

        /// <summary>
        /// Combines 2 lists into one list. The second list will be attached to the end of the first list.
        /// </summary>
        /// <param name="frontListBlockIndex">The index of the start block of the first list.</param>
        /// <param name="backListBlockIndex">The index of the start block of the second list.</param>
        public void Join(int frontListBlockIndex, int backListBlockIndex)
        {
            int frontEntryIndex = BlockToEntryIndex(frontListBlockIndex);
            int backEntryIndex = BlockToEntryIndex(backListBlockIndex);

            int frontTailIndex = GetListTail(frontEntryIndex);

            AllocationTableEntry frontTail = ReadEntry(frontTailIndex);
            AllocationTableEntry backHead = ReadEntry(backEntryIndex);

            frontTail.SetNext(backEntryIndex);
            backHead.SetPrev(frontTailIndex);

            WriteEntry(frontTailIndex, frontTail);
            WriteEntry(backEntryIndex, backHead);
        }

        /// <summary>
        /// Trims an existing list to the specified length and returns the excess blocks as a new list.
        /// </summary>
        /// <param name="listHeadBlockIndex">The starting block of the list to trim.</param>
        /// <param name="newListLength">The length in blocks that the list will be shortened to.</param>
        /// <returns>The index of the head node of the removed blocks.</returns>
        public int Trim(int listHeadBlockIndex, int newListLength)
        {
            int blocksRemaining = newListLength;
            int nextEntry = BlockToEntryIndex(listHeadBlockIndex);
            int listAIndex = -1;
            int listBIndex = -1;

            while (blocksRemaining > 0)
            {
                if (nextEntry == 0)
                {
                    return -1;
                }

                int currentEntryIndex = nextEntry;

                ReadEntry(EntryIndexToBlock(currentEntryIndex), out int nextBlock, out int _, out int segmentLength);

                nextEntry = BlockToEntryIndex(nextBlock);

                if (segmentLength == blocksRemaining)
                {
                    listAIndex = currentEntryIndex;
                    listBIndex = nextEntry;
                }
                else if (segmentLength > blocksRemaining)
                {
                    Split(EntryIndexToBlock(currentEntryIndex), blocksRemaining);

                    listAIndex = currentEntryIndex;
                    listBIndex = currentEntryIndex + blocksRemaining;
                }

                blocksRemaining -= segmentLength;
            }

            if (listAIndex == -1 || listBIndex == -1) return -1;

            AllocationTableEntry listANode = ReadEntry(listAIndex);
            AllocationTableEntry listBNode = ReadEntry(listBIndex);

            listANode.SetNext(0);
            listBNode.MakeListStart();

            WriteEntry(listAIndex, listANode);
            WriteEntry(listBIndex, listBNode);

            return EntryIndexToBlock(listBIndex);
        }

        /// <summary>
        /// Splits a single list segment into 2 segments. The sequence of blocks in the full list will remain the same.
        /// </summary>
        /// <param name="segmentBlockIndex">The block index of the segment to split.</param>
        /// <param name="firstSubSegmentLength">The length of the first subsegment.</param>
        public void Split(int segmentBlockIndex, int firstSubSegmentLength)
        {
            Debug.Assert(firstSubSegmentLength > 0);

            int segAIndex = BlockToEntryIndex(segmentBlockIndex);

            AllocationTableEntry segA = ReadEntry(segAIndex);
            if (!segA.IsMultiBlockSegment()) throw new ArgumentException("Cannot split a single-entry segment.");

            AllocationTableEntry segARange = ReadEntry(segAIndex + 1);
            int originalLength = segARange.GetNext() - segARange.GetPrev() + 1;

            if (firstSubSegmentLength >= originalLength)
            {
                throw new ArgumentOutOfRangeException(nameof(firstSubSegmentLength),
                    $"Requested sub-segment length ({firstSubSegmentLength}) must be less than the full segment length ({originalLength})");
            }

            int segBIndex = segAIndex + firstSubSegmentLength;

            int segALength = firstSubSegmentLength;
            int segBLength = originalLength - segALength;

            var segB = new AllocationTableEntry();

            // Insert segment B between segments A and C
            segB.SetPrev(segAIndex);
            segB.SetNext(segA.GetNext());
            segA.SetNext(segBIndex);

            if (!segB.IsListEnd())
            {
                AllocationTableEntry segC = ReadEntry(segB.GetNext());
                segC.SetPrev(segBIndex);
                WriteEntry(segB.GetNext(), segC);
            }

            // Write the new range entries if needed
            if (segBLength > 1)
            {
                segB.MakeMultiBlockSegment();

                var segBRange = new AllocationTableEntry();
                segBRange.SetRange(segBIndex, segBIndex + segBLength - 1);

                WriteEntry(segBIndex + 1, segBRange);
                WriteEntry(segBIndex + segBLength - 1, segBRange);
            }

            WriteEntry(segBIndex, segB);

            if (segALength == 1)
            {
                segA.MakeSingleBlockSegment();
            }
            else
            {
                segARange.SetRange(segAIndex, segAIndex + segALength - 1);

                WriteEntry(segAIndex + 1, segARange);
                WriteEntry(segAIndex + segALength - 1, segARange);
            }

            WriteEntry(segAIndex, segA);
        }

        public int GetFreeListLength()
        {
            int freeListStart = GetFreeListBlockIndex();

            if (freeListStart == -1) return 0;

            return GetListLength(freeListStart);
        }

        public int GetListLength(int blockIndex)
        {
            int index = blockIndex;
            int totalLength = 0;

            int tableSize = Header.AllocationTableBlockCount;
            int nodesIterated = 0;

            while (index != -1)
            {
                ReadEntry(index, out index, out int _, out int length);

                totalLength += length;
                nodesIterated++;

                if (nodesIterated > tableSize)
                {
                    throw new InvalidDataException("Cycle detected in allocation table.");
                }
            }

            return totalLength;
        }

        public void FsTrimList(int blockIndex)
        {
            int index = blockIndex;

            int tableSize = Header.AllocationTableBlockCount;
            int nodesIterated = 0;

            while (index != -1)
            {
                ReadEntry(index, out int next, out int _, out int length);

                if (length > 3)
                {
                    int fillOffset = BlockToEntryIndex(index + 2) * EntrySize;
                    int fillLength = (length - 3) * EntrySize;

                    BaseStorage.Slice(fillOffset, fillLength).Fill(SaveDataFileSystem.TrimFillValue);
                }

                nodesIterated++;

                if (nodesIterated > tableSize)
                {
                    return;
                }

                index = next;
            }
        }

        public void FsTrim()
        {
            int tableSize = BlockToEntryIndex(Header.AllocationTableBlockCount) * EntrySize;
            BaseStorage.Slice(tableSize).Fill(SaveDataFileSystem.TrimFillValue);
        }

        private void ReadEntries(int entryIndex, Span<AllocationTableEntry> entries)
        {
            Debug.Assert(entries.Length >= 2);

            bool isLastBlock = entryIndex == BlockToEntryIndex(Header.AllocationTableBlockCount) - 1;
            int entriesToRead = isLastBlock ? 1 : 2;
            int offset = entryIndex * EntrySize;

            Span<byte> buffer = MemoryMarshal.Cast<AllocationTableEntry, byte>(entries.Slice(0, entriesToRead));

            BaseStorage.Read(offset, buffer).ThrowIfFailure();
        }

        private AllocationTableEntry ReadEntry(int entryIndex)
        {
            Span<byte> bytes = stackalloc byte[EntrySize];
            int offset = entryIndex * EntrySize;

            BaseStorage.Read(offset, bytes).ThrowIfFailure();

            return GetEntryFromBytes(bytes);
        }

        private void WriteEntry(int entryIndex, AllocationTableEntry entry)
        {
            Span<byte> bytes = stackalloc byte[EntrySize];
            int offset = entryIndex * EntrySize;

            ref AllocationTableEntry newEntry = ref GetEntryFromBytes(bytes);
            newEntry = entry;

            BaseStorage.Write(offset, bytes).ThrowIfFailure();
        }

        // ReSharper disable once UnusedMember.Local
        private int GetListHead(int entryIndex)
        {
            int headIndex = entryIndex;
            int tableSize = Header.AllocationTableBlockCount;
            int nodesTraversed = 0;

            AllocationTableEntry entry = ReadEntry(entryIndex);

            while (!entry.IsListStart())
            {
                nodesTraversed++;
                headIndex = entry.Prev & 0x7FFFFFFF;
                entry = ReadEntry(headIndex);

                if (nodesTraversed > tableSize)
                {
                    throw new InvalidDataException("Cycle detected in allocation table.");
                }
            }

            return headIndex;
        }

        private int GetListTail(int entryIndex)
        {
            int tailIndex = entryIndex;
            int tableSize = Header.AllocationTableBlockCount;
            int nodesTraversed = 0;

            AllocationTableEntry entry = ReadEntry(entryIndex);

            while (!entry.IsListEnd())
            {
                nodesTraversed++;
                tailIndex = entry.Next & 0x7FFFFFFF;
                entry = ReadEntry(tailIndex);

                if (nodesTraversed > tableSize)
                {
                    throw new InvalidDataException("Cycle detected in allocation table.");
                }
            }

            return tailIndex;
        }

        private static ref AllocationTableEntry GetEntryFromBytes(Span<byte> entry)
        {
            return ref MemoryMarshal.Cast<byte, AllocationTableEntry>(entry)[0];
        }

        private static int EntryIndexToBlock(int entryIndex) => entryIndex - 1;
        private static int BlockToEntryIndex(int blockIndex) => blockIndex + 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AllocationTableEntry
    {
        public int Prev;
        public int Next;

        public int GetPrev()
        {
            return Prev & 0x7FFFFFFF;
        }

        public int GetNext()
        {
            return Next & 0x7FFFFFFF;
        }

        public bool IsListStart()
        {
            return Prev == int.MinValue;
        }

        public bool IsListEnd()
        {
            return (Next & 0x7FFFFFFF) == 0;
        }

        public bool IsMultiBlockSegment()
        {
            return Next < 0;
        }

        public void MakeMultiBlockSegment()
        {
            Next |= unchecked((int)0x80000000);
        }

        public void MakeSingleBlockSegment()
        {
            Next &= 0x7FFFFFFF;
        }

        public bool IsSingleBlockSegment()
        {
            return Next >= 0;
        }

        public void MakeListStart()
        {
            Prev = int.MinValue;
        }

        public bool IsRangeEntry()
        {
            return Prev != int.MinValue && Prev < 0;
        }

        public void MakeRangeEntry()
        {
            Prev |= unchecked((int)0x80000000);
        }

        public void SetNext(int value)
        {
            Debug.Assert(value >= 0);

            Next = Next & unchecked((int)0x80000000) | value;
        }

        public void SetPrev(int value)
        {
            Debug.Assert(value >= 0);

            Prev = value;
        }

        public void SetRange(int startIndex, int endIndex)
        {
            Debug.Assert(startIndex > 0);
            Debug.Assert(endIndex > 0);

            Next = endIndex;
            Prev = startIndex;
            MakeRangeEntry();
        }
    }

    public class AllocationTableHeader
    {
        public long BlockSize { get; }
        public long AllocationTableOffset { get; }
        public int AllocationTableBlockCount { get; }
        public long DataOffset { get; }
        public long DataBlockCount { get; }
        public int DirectoryTableBlock { get; }
        public int FileTableBlock { get; }

        public AllocationTableHeader(IStorage storage)
        {
            var reader = new BinaryReader(storage.AsStream());

            BlockSize = reader.ReadInt64();

            AllocationTableOffset = reader.ReadInt64();
            AllocationTableBlockCount = reader.ReadInt32();
            reader.BaseStream.Position += 4;

            DataOffset = reader.ReadInt64();
            DataBlockCount = reader.ReadInt32();
            reader.BaseStream.Position += 4;

            DirectoryTableBlock = reader.ReadInt32();
            FileTableBlock = reader.ReadInt32();
        }
    }
}
