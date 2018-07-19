using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace libhac.Savefile
{
    public class RemapStream : Stream
    {
        private long _position;
        private Stream BaseStream { get; }
        public MapEntry[] MapEntries { get; set; }
        public MapEntry CurrentEntry { get; set; }
        public RemapSegment[] Segments { get; set; }

        public RemapStream(Stream baseStream, MapEntry[] entries, int segmentCount)
        {
            BaseStream = baseStream;
            MapEntries = entries;
            Segments = new RemapSegment[segmentCount];

            int entryIdx = 0;
            for (int i = 0; i < segmentCount; i++)
            {
                var seg = new RemapSegment();
                seg.Entries.Add(MapEntries[entryIdx]);
                seg.Offset = MapEntries[entryIdx].VirtualOffset;
                MapEntries[entryIdx].Segment = seg;
                entryIdx++;

                while (entryIdx < MapEntries.Length &&
                       MapEntries[entryIdx - 1].VirtualOffsetEnd == MapEntries[entryIdx].VirtualOffset)
                {
                    MapEntries[entryIdx].Segment = seg;
                    MapEntries[entryIdx - 1].Next = MapEntries[entryIdx];
                    seg.Entries.Add(MapEntries[entryIdx]);
                    entryIdx++;
                }

                seg.Length = seg.Entries[seg.Entries.Count - 1].VirtualOffsetEnd - seg.Entries[0].VirtualOffset;
                Segments[i] = seg;
            }

            CurrentEntry = GetMapEntry(0);
            UpdateBaseStreamPosition();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (CurrentEntry == null) return 0;
            long remaining = CurrentEntry.Segment.Offset + CurrentEntry.Segment.Length - Position;
            if (remaining <= 0) return 0;
            if (remaining < count) count = (int)remaining;

            var toOutput = count;
            int pos = 0;

            while (toOutput > 0)
            {
                var remainInEntry = CurrentEntry.VirtualOffsetEnd - Position;
                int toRead = (int)Math.Min(toOutput, remainInEntry);
                BaseStream.Read(buffer, pos, toRead);
                pos += toRead;
                toOutput -= toRead;
                Position += toRead;
            }

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = Length - offset;
                    break;
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        private MapEntry GetMapEntry(long offset)
        {
            // todo: is O(n) search a possible performance issue?
            var entry = MapEntries.FirstOrDefault(x => offset >= x.VirtualOffset && offset < x.VirtualOffsetEnd);
            if (entry == null) throw new ArgumentOutOfRangeException(nameof(offset));
            return entry;
        }

        private void UpdateBaseStreamPosition()
        {
            // At end of virtual stream
            if (CurrentEntry == null) return;
            var entryOffset = Position - CurrentEntry.VirtualOffset;
            BaseStream.Position = CurrentEntry.PhysicalOffset + entryOffset;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; } = -1;

        public override long Position
        {
            get => _position;
            set
            {
                // Avoid doing a search when reading sequentially
                if (CurrentEntry != null && value == CurrentEntry.VirtualOffsetEnd)
                {
                    CurrentEntry = CurrentEntry.Next;
                }
                else if (CurrentEntry == null || value < CurrentEntry.VirtualOffset || value > CurrentEntry.VirtualOffsetEnd)
                {
                    CurrentEntry = GetMapEntry(value);
                }

                _position = value;
                UpdateBaseStreamPosition();
            }
        }
    }

    public class RemapSegment
    {
        public List<MapEntry> Entries { get; } = new List<MapEntry>();
        public long Offset { get; internal set; }
        public long Length { get; internal set; }
    }
}
