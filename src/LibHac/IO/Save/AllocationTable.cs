using System;
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

        public AllocationTable(IStorage storage, IStorage header)
        {
            BaseStorage = storage;
            HeaderStorage = header;
            Header = new AllocationTableHeader(HeaderStorage);
        }

        public void ReadEntry(int index, out AllocationTableEntry entry)
        {
            Span<byte> bytes = stackalloc byte[EntrySize];
            int offset = index * EntrySize;

            BaseStorage.Read(bytes, offset);

            entry = GetEntryFromBytes(bytes);
        }

        public void WriteEntry(int index, ref AllocationTableEntry entry)
        {
            Span<byte> bytes = stackalloc byte[EntrySize];
            int offset = index * EntrySize;

            ref AllocationTableEntry newEntry = ref GetEntryFromBytes(bytes);
            newEntry = entry;

            BaseStorage.Write(bytes, offset);
        }

        private ref AllocationTableEntry GetEntryFromBytes(Span<byte> entry)
        {
            return ref MemoryMarshal.Cast<byte, AllocationTableEntry>(entry)[0];
        }

        public IStorage GetBaseStorage() => BaseStorage.AsReadOnly();
        public IStorage GetHeaderStorage() => HeaderStorage.AsReadOnly();
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
        public long AllocationTableBlockCount { get; }
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
