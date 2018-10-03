using System;
using System.IO;

namespace LibHac.Savefile
{
    public class JournalStream : Stream
    {
        private long _position;
        private Stream BaseStream { get; }
        public MappingEntry[] Map { get; }
        public int BlockSize { get; }
        private MappingEntry CurrentMapEntry { get; set; }

        public JournalStream(Stream baseStream, MappingEntry[] map, int blockSize)
        {
            BaseStream = baseStream;
            Map = map;
            BlockSize = blockSize;
            Length = map.Length * BlockSize;

            CurrentMapEntry = Map[0];
            BaseStream.Position = CurrentMapEntry.PhysicalIndex * BlockSize;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = Length - Position;
            if (remaining <= 0) return 0;
            if (remaining < count) count = (int)remaining;

            int toOutput = count;
            int outPos = offset;

            while (toOutput > 0)
            {
                long remainInEntry = BlockSize - Position % BlockSize;
                int toRead = (int)Math.Min(toOutput, remainInEntry);
                BaseStream.Read(buffer, outPos, toRead);

                outPos += toRead;
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

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; }
        public override long Position
        {
            get => _position;
            set
            {
                _position = value;
                if (value >= Length) return;
                long currentBlock = value / BlockSize;
                long blockPos = value % BlockSize;
                CurrentMapEntry = Map[currentBlock];
                BaseStream.Position = CurrentMapEntry.PhysicalIndex * BlockSize + blockPos;
            }
        }

        public static MappingEntry[] ReadMappingEntries(Stream mapTable, int count)
        {
            var tableReader = new BinaryReader(mapTable);
            var map = new MappingEntry[count];

            for (int i = 0; i < count; i++)
            {
                var entry = new MappingEntry
                {
                    VirtualIndex = i,
                    PhysicalIndex = tableReader.ReadInt32() & 0x7FFFFFFF
                };

                map[i] = entry;
                tableReader.BaseStream.Position += 4;
            }

            return map;
        }
    }

    public class MappingEntry
    {
        public int PhysicalIndex { get; set; }
        public int VirtualIndex { get; set; }
    }
}
