﻿using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac.IO.Save
{
    public class RemapStorage : Storage
    {
        private Storage BaseStorage { get; }
        private RemapHeader Header { get; }
        public MapEntry[] MapEntries { get; set; }
        public RemapSegment[] Segments { get; set; }

        public override long Length { get; } = -1;

        /// <summary>
        /// Creates a new <see cref="RemapStorage"/>
        /// </summary>
        /// <param name="storage">A <see cref="Storage"/> of the main data of the RemapStream.
        /// The <see cref="RemapStorage"/> object takes complete ownership of the Stream.</param>
        /// <param name="header">The header for this RemapStorage.</param>
        /// <param name="mapEntries">The remapping entries for this RemapStorage.</param>
        public RemapStorage(Storage storage, RemapHeader header, MapEntry[] mapEntries)
        {
            BaseStorage = storage;
            Header = header;
            MapEntries = mapEntries;

            Segments = InitSegments(Header, MapEntries);
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            MapEntry entry = GetMapEntry(offset);

            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                long entryPos = inPos - entry.VirtualOffset;

                int bytesToRead = (int)Math.Min(entry.VirtualOffsetEnd - inPos, remaining);
                int bytesRead = BaseStorage.Read(destination.Slice(outPos, bytesToRead), entry.PhysicalOffset + entryPos);

                outPos += bytesRead;
                inPos += bytesRead;
                remaining -= bytesRead;

                if (inPos >= entry.VirtualOffsetEnd)
                {
                    entry = entry.Next;
                }
            }

            return outPos;
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
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

        private int GetSegmentFromVirtualOffset(long virtualOffset)
        {
            return (int)((ulong)virtualOffset >> (64 - Header.SegmentBits));
        }

        private long GetOffsetFromVirtualOffset(long virtualOffset)
        {
            return virtualOffset & GetOffsetMask();
        }

        private long ToVirtualOffset(int segment, long offset)
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
