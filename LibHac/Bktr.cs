using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LibHac
{
    public class Bktr : Stream
    {
        private long _position;
        public RelocationBlock RelocationBlock { get; }
        private List<RelocationEntry> RelocationEntries { get; } = new List<RelocationEntry>();
        private List<long> RelocationOffsets { get; }

        private Stream Patch { get; }
        private Stream Base { get; }
        private RelocationEntry CurrentEntry { get; set; }

        public Bktr(Stream patchRomfs, Stream baseRomfs, NcaSection section)
        {
            if (section.Type != SectionType.Bktr) throw new ArgumentException("Section is not of type BKTR");
            Patch = patchRomfs ?? throw new NullReferenceException($"{nameof(patchRomfs)} cannot be null");
            Base = baseRomfs ?? throw new NullReferenceException($"{nameof(baseRomfs)} cannot be null");

            IvfcLevelHeader level5 = section.Header.Bktr.IvfcHeader.LevelHeaders[5];
            Length = level5.LogicalOffset + level5.HashDataSize;

            using (var reader = new BinaryReader(patchRomfs, Encoding.Default, true))
            {
                patchRomfs.Position = section.Header.Bktr.RelocationHeader.Offset;
                RelocationBlock = new RelocationBlock(reader);
            }

            foreach (RelocationBucket bucket in RelocationBlock.Buckets)
            {
                RelocationEntries.AddRange(bucket.Entries);
            }

            for (int i = 0; i < RelocationEntries.Count - 1; i++)
            {
                RelocationEntries[i].Next = RelocationEntries[i + 1];
                RelocationEntries[i].VirtOffsetEnd = RelocationEntries[i + 1].VirtOffset;
            }

            RelocationEntries[RelocationEntries.Count - 1].VirtOffsetEnd = level5.LogicalOffset + level5.HashDataSize;
            RelocationOffsets = RelocationEntries.Select(x => x.VirtOffset).ToList();

            CurrentEntry = GetRelocationEntry(0);
            UpdateSourceStreamPositions();
        }

        private RelocationEntry GetRelocationEntry(long offset)
        {
            var index = RelocationOffsets.BinarySearch(offset);
            if (index < 0) index = ~index - 1;
            return RelocationEntries[index];
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = Length - Position;
            if (remaining <= 0) return 0;
            if (remaining < count) count = (int)remaining;

            var toOutput = count;
            int pos = 0;

            while (toOutput > 0)
            {
                var remainInEntry = CurrentEntry.VirtOffsetEnd - Position;
                int toRead = (int)Math.Min(toOutput, remainInEntry);
                ReadCurrent(buffer, pos, toRead);
                pos += toRead;
                toOutput -= toRead;
            }

            return count;
        }

        private void ReadCurrent(byte[] buffer, int offset, int count)
        {
            if (CurrentEntry.IsPatch)
            {
                Patch.Read(buffer, offset, count);
            }
            else
            {
                Base.Read(buffer, offset, count);
            }

            Position += count;
        }

        private void UpdateSourceStreamPositions()
        {
            // At end of virtual stream
            if (CurrentEntry == null) return;

            var entryOffset = Position - CurrentEntry.VirtOffset;

            if (CurrentEntry.IsPatch)
            {
                Patch.Position = CurrentEntry.PhysOffset + entryOffset;
            }
            else
            {
                Base.Position = CurrentEntry.PhysOffset + entryOffset;
            }
        }

        public override long Position
        {
            get => _position;
            set
            {
                if (value > Length) throw new IndexOutOfRangeException();

                // Avoid doing a search when reading sequentially
                if (CurrentEntry != null && value == CurrentEntry.VirtOffsetEnd)
                {
                    CurrentEntry = CurrentEntry.Next;
                }
                else if (CurrentEntry == null || value < CurrentEntry.VirtOffset || value > CurrentEntry.VirtOffsetEnd)
                {
                    CurrentEntry = GetRelocationEntry(value);
                }

                _position = value;
                UpdateSourceStreamPositions();
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

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override long Length { get; }
        public override bool CanSeek => true;
    }
}
