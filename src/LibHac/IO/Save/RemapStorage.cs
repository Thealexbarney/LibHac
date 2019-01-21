using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac.IO.Save
{
    public class RemapStorage : StorageBase
    {
        private IStorage BaseStorage { get; }
        private IStorage HeaderStorage { get; }
        private IStorage MapEntryStorage { get; }

        private RemapHeader Header { get; }
        public MapEntry[] MapEntries { get; set; }
        public RemapSegment[] Segments { get; set; }

        public override long Length { get; } = -1;

        /// <summary>
        /// Creates a new <see cref="RemapStorage"/>
        /// </summary>
        /// <param name="storage">A <see cref="IStorage"/> of the main data of the RemapStream.
        /// The <see cref="RemapStorage"/> object assumes complete ownership of the Storage.</param>
        /// <param name="header">The header for this RemapStorage.</param>
        /// <param name="mapEntries">The remapping entries for this RemapStorage.</param>
        /// <param name="leaveOpen"><see langword="true"/> to leave the storage open after the <see cref="RemapStorage"/> object is disposed; otherwise, <see langword="false"/>.</param>
        public RemapStorage(IStorage storage, IStorage header, IStorage mapEntries, bool leaveOpen)
        {
            BaseStorage = storage;
            HeaderStorage = header;
            MapEntryStorage = mapEntries;

            Header = new RemapHeader(HeaderStorage);

            MapEntries = new MapEntry[Header.MapEntryCount];
            var reader = new BinaryReader(MapEntryStorage.AsStream());

            for (int i = 0; i < Header.MapEntryCount; i++)
            {
                MapEntries[i] = new MapEntry(reader);
            }

            if (!leaveOpen) ToDispose.Add(BaseStorage);

            Segments = InitSegments(Header, MapEntries);
        }

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            MapEntry entry = GetMapEntry(offset);

            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                long entryPos = inPos - entry.VirtualOffset;

                int bytesToRead = (int)Math.Min(entry.VirtualOffsetEnd - inPos, remaining);
                BaseStorage.Read(destination.Slice(outPos, bytesToRead), entry.PhysicalOffset + entryPos);

                outPos += bytesToRead;
                inPos += bytesToRead;
                remaining -= bytesToRead;

                if (inPos >= entry.VirtualOffsetEnd)
                {
                    entry = entry.Next;
                }
            }
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            MapEntry entry = GetMapEntry(offset);

            long inPos = offset;
            int outPos = 0;
            int remaining = source.Length;

            while (remaining > 0)
            {
                long entryPos = inPos - entry.VirtualOffset;

                int bytesToWrite = (int)Math.Min(entry.VirtualOffsetEnd - inPos, remaining);
                BaseStorage.Write(source.Slice(outPos, bytesToWrite), entry.PhysicalOffset + entryPos);

                outPos += bytesToWrite;
                inPos += bytesToWrite;
                remaining -= bytesToWrite;

                if (inPos >= entry.VirtualOffsetEnd)
                {
                    entry = entry.Next;
                }
            }
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }

        public IStorage GetBaseStorage() => BaseStorage.AsReadOnly();
        public IStorage GetHeaderStorage() => HeaderStorage.AsReadOnly();
        public IStorage GetMapEntryStorage() => MapEntryStorage.AsReadOnly();

        private static RemapSegment[] InitSegments(RemapHeader header, MapEntry[] mapEntries)
        {
            var segments = new RemapSegment[header.MapSegmentCount];
            int entryIdx = 0;

            for (int i = 0; i < header.MapSegmentCount; i++)
            {
                var seg = new RemapSegment();
                seg.Entries.Add(mapEntries[entryIdx]);
                seg.Offset = mapEntries[entryIdx].VirtualOffset;
                mapEntries[entryIdx].Segment = seg;
                entryIdx++;

                while (entryIdx < mapEntries.Length &&
                       mapEntries[entryIdx - 1].VirtualOffsetEnd == mapEntries[entryIdx].VirtualOffset)
                {
                    mapEntries[entryIdx].Segment = seg;
                    mapEntries[entryIdx - 1].Next = mapEntries[entryIdx];
                    seg.Entries.Add(mapEntries[entryIdx]);
                    entryIdx++;
                }

                seg.Length = seg.Entries[seg.Entries.Count - 1].VirtualOffsetEnd - seg.Entries[0].VirtualOffset;
                segments[i] = seg;
            }

            return segments;
        }

        private MapEntry GetMapEntry(long offset)
        {
            int segmentIdx = GetSegmentFromVirtualOffset(offset);

            if (segmentIdx < Segments.Length)
            {
                RemapSegment segment = Segments[segmentIdx];

                foreach (MapEntry entry in segment.Entries)
                {
                    if (entry.VirtualOffsetEnd > offset) return entry;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        public int GetSegmentFromVirtualOffset(long virtualOffset)
        {
            return (int)((ulong)virtualOffset >> (64 - Header.SegmentBits));
        }

        public long GetOffsetFromVirtualOffset(long virtualOffset)
        {
            return virtualOffset & GetOffsetMask();
        }

        public long ToVirtualOffset(int segment, long offset)
        {
            long seg = (segment << (64 - Header.SegmentBits)) & GetSegmentMask();
            long off = offset & GetOffsetMask();
            return seg | off;
        }

        private long GetOffsetMask()
        {
            return (1 << (64 - Header.SegmentBits)) - 1;
        }

        private long GetSegmentMask()
        {
            return ~GetOffsetMask();
        }
    }

    public class RemapHeader
    {
        public string Magic { get; }
        public uint Verison { get; }
        public int MapEntryCount { get; }
        public int MapSegmentCount { get; }
        public int SegmentBits { get; }

        public RemapHeader(IStorage storage)
        {
            var reader = new BinaryReader(storage.AsStream());

            Magic = reader.ReadAscii(4);
            Verison = reader.ReadUInt32();
            MapEntryCount = reader.ReadInt32();
            MapSegmentCount = reader.ReadInt32();
            SegmentBits = reader.ReadInt32();
        }
    }

    public class MapEntry
    {
        public long VirtualOffset { get; }
        public long PhysicalOffset { get; }
        public long Size { get; }
        public int Alignment { get; }
        public int Field1C { get; }

        public long VirtualOffsetEnd => VirtualOffset + Size;
        public long PhysicalOffsetEnd => PhysicalOffset + Size;
        internal RemapSegment Segment { get; set; }
        internal MapEntry Next { get; set; }

        public MapEntry(BinaryReader reader)
        {
            VirtualOffset = reader.ReadInt64();
            PhysicalOffset = reader.ReadInt64();
            Size = reader.ReadInt64();
            Alignment = reader.ReadInt32();
            Field1C = reader.ReadInt32();
        }
    }

    public class RemapSegment
    {
        public List<MapEntry> Entries { get; } = new List<MapEntry>();
        public long Offset { get; internal set; }
        public long Length { get; internal set; }
    }
}
