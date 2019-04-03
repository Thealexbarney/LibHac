using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace LibHac.IO.Save
{
    public class AllocationTable
    {
        private const int EntrySize = 8;

        private IStorage BaseStorage { get; }
        private IStorage HeaderStorage { get; }

        public AllocationTableHeader Header { get; }

        public IStorage GetBaseStorage() => BaseStorage.AsReadOnly();
        public IStorage GetHeaderStorage() => HeaderStorage.AsReadOnly();

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
            }
            else
            {
                length = entries[1].Next - entryIndex;
            }

            if (entries[0].IsListEnd())
            {
                next = -1;
            }
            else
            {
                next = EntryIndexToBlock(entries[0].Next & 0x7FFFFFFF);
            }

            if (entries[0].IsListStart())
            {
                previous = -1;
            }
            else
            {
                previous = EntryIndexToBlock(entries[0].Prev & 0x7FFFFFFF);
            }
        }

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

        private void ReadEntries(int entryIndex, Span<AllocationTableEntry> entries)
        {
            Debug.Assert(entries.Length >= 2);

            bool isLastBlock = entryIndex == BlockToEntryIndex(Header.AllocationTableBlockCount) - 1;
            int entriesToRead = isLastBlock ? 1 : 2;
            int offset = entryIndex * EntrySize;

            Span<byte> buffer = MemoryMarshal.Cast<AllocationTableEntry, byte>(entries.Slice(0, entriesToRead));

            BaseStorage.Read(buffer, offset);
        }

        private AllocationTableEntry ReadEntry(int entryIndex)
        {
            Span<byte> bytes = stackalloc byte[EntrySize];
            int offset = entryIndex * EntrySize;

            BaseStorage.Read(bytes, offset);

            return GetEntryFromBytes(bytes);
        }

        private void WriteEntry(int entryIndex, AllocationTableEntry entry)
        {
            Span<byte> bytes = stackalloc byte[EntrySize];
            int offset = entryIndex * EntrySize;

            ref AllocationTableEntry newEntry = ref GetEntryFromBytes(bytes);
            newEntry = entry;

            BaseStorage.Write(bytes, offset);
        }

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
