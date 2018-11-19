using System;
using System.IO;

namespace LibHac.IO.Save
{
    public class JournalStorage : Storage
    {
        private IStorage BaseStorage { get; }
        private IStorage HeaderStorage { get; }
        public JournalMap Map { get; }

        public JournalHeader Header { get; }

        public int BlockSize { get; }
        public override long Length { get; }

        public JournalStorage(IStorage baseStorage, IStorage header, JournalMapParams mapInfo, bool leaveOpen)
        {
            BaseStorage = baseStorage;
            HeaderStorage = header;
            Header = new JournalHeader(HeaderStorage);

            IStorage mapHeader = header.Slice(0x20, 0x10);
            Map = new JournalMap(mapHeader, mapInfo);

            BlockSize = (int)Header.BlockSize;
            Length = Header.TotalSize - Header.JournalSize;

            if (!leaveOpen) ToDispose.Add(baseStorage);
        }

        protected override void ReadImpl(Span<byte> destination, long offset)
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

                BaseStorage.Read(destination.Slice(outPos, bytesToRead), physicalOffset);

                outPos += bytesToRead;
                inPos += bytesToRead;
                remaining -= bytesToRead;
            }
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

        public IStorage GetBaseStorage() => BaseStorage.WithAccess(FileAccess.Read);
        public IStorage GetHeaderStorage() => HeaderStorage.WithAccess(FileAccess.Read);
    }

    public class JournalHeader
    {
        public string Magic { get; }
        public uint Version { get; }
        public long TotalSize { get; }
        public long JournalSize { get; }
        public long BlockSize { get; }

        public JournalHeader(IStorage storage)
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
