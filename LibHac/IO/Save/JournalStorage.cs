using System;
using System.IO;

namespace LibHac.IO.Save
{
    public class JournalStorage : Storage
    {
        public Storage BaseStorage { get; }
        public Storage HeaderStorage { get; }
        public JournalMap Map { get; }

        public JournalHeader Header { get; }

        public int BlockSize { get; }
        public override long Length { get; }

        public JournalStorage(Storage baseStorage, Storage header, JournalMapParams mapInfo, bool leaveOpen)
        {
            BaseStorage = baseStorage;
            HeaderStorage = header;
            Header = new JournalHeader(HeaderStorage);

            Storage mapHeader = header.Slice(0x20, 0x10);
            Map = new JournalMap(mapHeader, mapInfo);

            BlockSize = (int)Header.BlockSize;
            Length = Header.TotalSize - Header.JournalSize;

            if (!leaveOpen) ToDispose.Add(baseStorage);
        }

        protected override int ReadImpl(Span<byte> destination, long offset)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                int blockNum = (int)(inPos / BlockSize);
                int blockPos = (int)(inPos % BlockSize);

                long physicalOffset = Map.GetPhysicalBlock(blockNum) * BlockSize + blockPos;

                int bytesToRead = Math.Min(remaining, BlockSize - blockPos);

                int bytesRead = BaseStorage.Read(destination.Slice(outPos, bytesToRead), physicalOffset);

                outPos += bytesRead;
                inPos += bytesRead;
                remaining -= bytesRead;
            }

            return outPos;
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = source.Length;

            while (remaining > 0)
            {
                int blockNum = (int)(inPos / BlockSize);
                int blockPos = (int)(inPos % BlockSize);

                long physicalOffset = Map.GetPhysicalBlock(blockNum) * BlockSize + blockPos;

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
    }

    public class JournalHeader
    {
        public string Magic { get; }
        public uint Version { get; }
        public long TotalSize { get; }
        public long JournalSize { get; }
        public long BlockSize { get; }

        public JournalHeader(Storage storage)
        {
            var reader = new BinaryReader(storage.AsStream());

            Magic = reader.ReadAscii(4);
            Version = reader.ReadUInt32();
            TotalSize = reader.ReadInt64();
            JournalSize = reader.ReadInt64();
            BlockSize = reader.ReadInt64();
        }
    }

    public class JournalMapEntry
    {
        public int PhysicalIndex { get; set; }
        public int VirtualIndex { get; set; }
    }
}
