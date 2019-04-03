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

        private void ReadEntries(int entryIndex, Span<AllocationTableEntry> entries)
        {
            Debug.Assert(entries.Length >= 2);

            bool isLastBlock = entryIndex == BlockToEntryIndex(Header.AllocationTableBlockCount) - 1;
            int entriesToRead = isLastBlock ? 1 : 2;
            int offset = entryIndex * EntrySize;

            Span<byte> buffer = MemoryMarshal.Cast<AllocationTableEntry, byte>(entries.Slice(0, entriesToRead));

            BaseStorage.Read(buffer, offset);
        }

        private void ReadEntry(int entryIndex, out AllocationTableEntry entry)
        {
            Span<byte> bytes = stackalloc byte[EntrySize];
            int offset = entryIndex * EntrySize;

            BaseStorage.Read(bytes, offset);

            entry = GetEntryFromBytes(bytes);
        }

        private void WriteEntry(int entryIndex, ref AllocationTableEntry entry)
        {
            Span<byte> bytes = stackalloc byte[EntrySize];
            int offset = entryIndex * EntrySize;

            ref AllocationTableEntry newEntry = ref GetEntryFromBytes(bytes);
            newEntry = entry;

            BaseStorage.Write(bytes, offset);
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

        public bool IsSingleBlockSegment()
        {
            return Next >= 0;
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
