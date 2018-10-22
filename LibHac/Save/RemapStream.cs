using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LibHac.Save
{
    public class RemapStream : Stream
    {
        private long _position;
        private Stream BaseStream { get; }
        private RemapSegment Segment { get; }
        private MapEntry CurrentEntry { get; set; }

        public RemapStream(Stream baseStream, RemapSegment segment)
        {
            BaseStream = baseStream;
            Segment = segment;
            CurrentEntry = segment.Entries[0];
            Length = segment.Length;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (CurrentEntry == null) return 0;
            long remaining = CurrentEntry.Segment.Offset + CurrentEntry.Segment.Length - Position;
            if (remaining <= 0) return 0;
            if (remaining < count) count = (int)remaining;

            int toOutput = count;
            int outPos = offset;

            while (toOutput > 0)
            {
                long remainInEntry = CurrentEntry.VirtualOffsetEnd - Position;
                int toRead = (int)Math.Min(toOutput, remainInEntry);
                BaseStream.Read(buffer, outPos, toRead);
                outPos += toRead;
                toOutput -= toRead;
                Position += toRead;
            }

            return count;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (CurrentEntry == null) throw new EndOfStreamException();

            long remaining = Math.Min(CurrentEntry.VirtualOffsetEnd - Position, count);
            if (remaining <= 0) return;

            int inPos = offset;

            while (remaining > 0)
            {
                long remainInEntry = CurrentEntry.VirtualOffsetEnd - Position;
                int toWrite = (int)Math.Min(remaining, remainInEntry);
                BaseStream.Write(buffer, inPos, toWrite);

                inPos += toWrite;
                remaining -= toWrite;
                Position += toWrite;
            }
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

        public override void Flush()
        {
            BaseStream.Flush();
        }

        private MapEntry GetMapEntry(long offset)
        {
            MapEntry entry = Segment.Entries.FirstOrDefault(x => offset >= x.VirtualOffset && offset < x.VirtualOffsetEnd);
            if (entry == null) throw new ArgumentOutOfRangeException(nameof(offset));
            return entry;
        }

        private void UpdateBaseStreamPosition()
        {
            // At end of virtual stream
            if (CurrentEntry == null) return;
            long entryOffset = Position - CurrentEntry.VirtualOffset;
            BaseStream.Position = CurrentEntry.PhysicalOffset + entryOffset;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; }

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
