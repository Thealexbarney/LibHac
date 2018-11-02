using System;
using System.IO;

namespace LibHac.IO.Save
{
    public class JournalStorage : Storage
    {
        private Storage BaseStorage { get; }
        public JournalMapEntry[] Map { get; }
        public int BlockSize { get; }
        public override long Length { get; }
        
        public JournalStorage(Storage baseStorage, JournalMapEntry[] map, int blockSize)
        {
            BaseStorage = baseStorage;
            Map = map;
            BlockSize = blockSize;
            Length = map.Length * BlockSize;
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                int blockNum = (int)(inPos / BlockSize);
                int blockPos = (int)(inPos % BlockSize);

                JournalMapEntry entry = Map[blockNum];
                long physicalOffset = entry.PhysicalIndex * BlockSize + blockPos;

                int bytesToRead = Math.Min(remaining, BlockSize - blockPos);

                int bytesRead = BaseStorage.Read(destination.Slice(outPos, bytesToRead), physicalOffset);

                outPos += bytesRead;
                inPos += bytesRead;
                remaining -= bytesRead;
            }

            return outPos;
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = source.Length;

            while (remaining > 0)
            {
                int blockNum = (int)(inPos / BlockSize);
                int blockPos = (int)(inPos % BlockSize);

                JournalMapEntry entry = Map[blockNum];
                long physicalOffset = entry.PhysicalIndex * BlockSize + blockPos;

                int bytesToWrite = Math.Min(remaining, BlockSize - blockPos);

                BaseStorage.Write(source.Slice(outPos, bytesToWrite), physicalOffset);

                outPos += bytesToWrite;
                inPos += bytesToWrite;
                remaining -= bytesToWrite;
            }
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }

        public static JournalMapEntry[] ReadMapEntries(Storage mapTable, int count)
        {
            var tableReader = new BinaryReader(mapTable.AsStream());
            var map = new JournalMapEntry[count];

            for (int i = 0; i < count; i++)
            {
                var entry = new JournalMapEntry
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

    public class JournalMapEntry
    {
        public int PhysicalIndex { get; set; }
        public int VirtualIndex { get; set; }
    }
}
