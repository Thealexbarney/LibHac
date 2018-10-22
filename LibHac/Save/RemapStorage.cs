using System;
using System.IO;
using LibHac.Streams;

namespace LibHac.Save
{
    public class RemapStorage
    {
        private SharedStreamSource StreamSource { get; }
        private RemapHeader Header { get; }
        public MapEntry[] MapEntries { get; set; }
        public RemapSegment[] Segments { get; set; }

        /// <summary>
        /// Creates a new <see cref="RemapStorage"/>
        /// </summary>
        /// <param name="data">A <see cref="Stream"/> of the main data of the RemapStream.
        /// The <see cref="RemapStorage"/> object takes complete ownership of the Stream.</param>
        /// <param name="header">The header for this RemapStorage.</param>
        /// <param name="mapEntries">The remapping entries for this RemapStorage.</param>
        public RemapStorage(Stream data, RemapHeader header, MapEntry[] mapEntries)
        {
            StreamSource = new SharedStreamSource(data);
            Header = header;
            MapEntries = mapEntries;

            Segments = InitSegments(Header, MapEntries);
        }

        public Stream OpenStream(long offset, long size)
        {
            int segmentIdx = GetSegmentFromVirtualOffset(offset);
            long segmentOffset = GetOffsetFromVirtualOffset(offset);

            if (segmentIdx > Segments.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            RemapSegment segment = Segments[GetSegmentFromVirtualOffset(offset)];

            if (segmentOffset > segment.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            Stream stream = new RemapStream(StreamSource.CreateStream(), segment);

            return new SubStream(stream, offset, size);
        }

        public Stream OpenSegmentStream(int segment)
        {
            long offset = ToVirtualOffset(segment, 0);
            long size = Segments[segment].Length;

            return OpenStream(offset, size);
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
}
