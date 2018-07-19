using System.IO;

namespace libhac.Savefile
{
    public class Header
    {
        public byte[] Cmac { get; set; }
        public FsLayout Layout { get; set; }

        public RemapHeader FileRemap { get; set; }
        public RemapHeader MetaRemap { get; set; }

        public MapEntry[] FileMapEntries { get; set; }
        public MapEntry[] MetaMapEntries { get; set; }

        public Header(BinaryReader reader)
        {
            Cmac = reader.ReadBytes(0x10);

            reader.BaseStream.Position = 0x100;
            Layout = new FsLayout(reader);

            reader.BaseStream.Position = 0x650;
            FileRemap = new RemapHeader(reader);
            reader.BaseStream.Position = 0x690;
            MetaRemap = new RemapHeader(reader);

            reader.BaseStream.Position = Layout.FileMapEntryOffset;
            FileMapEntries = new MapEntry[FileRemap.MapEntryCount];
            for (int i = 0; i < FileRemap.MapEntryCount; i++)
            {
                FileMapEntries[i] = new MapEntry(reader);
            }

            reader.BaseStream.Position = Layout.MetaMapEntryOffset;
            MetaMapEntries = new MapEntry[MetaRemap.MapEntryCount];
            for (int i = 0; i < MetaRemap.MapEntryCount; i++)
            {
                MetaMapEntries[i] = new MapEntry(reader);
            }
        }
    }

    public class FsLayout
    {
        public string Magic { get; set; }
        public uint MagicNum { get; set; }
        public byte[] Hash { get; set; }
        public long FileMapEntryOffset { get; set; }
        public long FileMapEntrySize { get; set; }
        public long MetaMapEntryOffset { get; set; }
        public long MetaMapEntrySize { get; set; }
        public long FileMapDataOffset { get; set; }
        public long FileMapDataSize { get; set; }
        public long DuplexL1OffsetA { get; set; }
        public long DuplexL1OffsetB { get; set; }
        public long DuplexL1Size { get; set; }
        public long DuplexDataOffsetA { get; set; }
        public long DuplexDataOffsetB { get; set; }
        public long DuplexDataSize { get; set; }
        public long JournalDataOffset { get; set; }
        public long JournalDataSizeA { get; set; }
        public long JournalDataSizeB { get; set; }
        public long SizeReservedArea { get; set; }
        public long OffsetL1Bitmap0 { get; set; }
        public long OffsetL1Bitmap1 { get; set; }
        public long SizeL1Bitmap { get; set; }
        public long MasterHashOffset { get; set; }
        public long FieldC8 { get; set; }
        public long MasterHashSize { get; set; }
        public long OffsetJournalTable { get; set; }
        public long SizeJournalTable { get; set; }
        public long JournalBitmapUpdatedPhysicalOffset { get; set; }
        public long JournalBitmapUpdatedPhysicalSize { get; set; }
        public long JournalBitmapUpdatedVirtualOffset { get; set; }
        public long JournalBitmapUpdatedVirtualSize { get; set; }
        public long JournalBitmapUnassignedOffset { get; set; }
        public long JournalBitmapUnassignedSize { get; set; }
        public long Layer1HashOffset { get; set; }
        public long Layer1HashSize { get; set; }
        public long Layer2HashOffset { get; set; }
        public long Layer2HashSize { get; set; }
        public long Layer3HashOffset { get; set; }
        public long Layer3HashSize { get; set; }
        public long Field148 { get; set; }
        public long Field150 { get; set; }
        public long Field158 { get; set; }

        public FsLayout(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            MagicNum = reader.ReadUInt32();
            Hash = reader.ReadBytes(0x20);
            FileMapEntryOffset = reader.ReadInt64();
            FileMapEntrySize = reader.ReadInt64();
            MetaMapEntryOffset = reader.ReadInt64();
            MetaMapEntrySize = reader.ReadInt64();
            FileMapDataOffset = reader.ReadInt64();
            FileMapDataSize = reader.ReadInt64();
            DuplexL1OffsetA = reader.ReadInt64();
            DuplexL1OffsetB = reader.ReadInt64();
            DuplexL1Size = reader.ReadInt64();
            DuplexDataOffsetA = reader.ReadInt64();
            DuplexDataOffsetB = reader.ReadInt64();
            DuplexDataSize = reader.ReadInt64();
            JournalDataOffset = reader.ReadInt64();
            JournalDataSizeA = reader.ReadInt64();
            JournalDataSizeB = reader.ReadInt64();
            SizeReservedArea = reader.ReadInt64();
            OffsetL1Bitmap0 = reader.ReadInt64();
            OffsetL1Bitmap1 = reader.ReadInt64();
            SizeL1Bitmap = reader.ReadInt64();
            MasterHashOffset = reader.ReadInt64();
            FieldC8 = reader.ReadInt64();
            MasterHashSize = reader.ReadInt64();
            OffsetJournalTable = reader.ReadInt64();
            SizeJournalTable = reader.ReadInt64();
            JournalBitmapUpdatedPhysicalOffset = reader.ReadInt64();
            JournalBitmapUpdatedPhysicalSize = reader.ReadInt64();
            JournalBitmapUpdatedVirtualOffset = reader.ReadInt64();
            JournalBitmapUpdatedVirtualSize = reader.ReadInt64();
            JournalBitmapUnassignedOffset = reader.ReadInt64();
            JournalBitmapUnassignedSize = reader.ReadInt64();
            Layer1HashOffset = reader.ReadInt64();
            Layer1HashSize = reader.ReadInt64();
            Layer2HashOffset = reader.ReadInt64();
            Layer2HashSize = reader.ReadInt64();
            Layer3HashOffset = reader.ReadInt64();
            Layer3HashSize = reader.ReadInt64();
            Field148 = reader.ReadInt64();
            Field150 = reader.ReadInt64();
            Field158 = reader.ReadInt64();
        }
    }

    public class RemapHeader
    {
        public string Magic { get; set; }
        public uint MagicNum { get; set; }
        public int MapEntryCount { get; set; }
        public int MapSegmentCount { get; set; }
        public int Field10 { get; set; }

        public RemapHeader(BinaryReader reader)
        {
            Magic = reader.ReadAscii(4);
            MagicNum = reader.ReadUInt32();
            MapEntryCount = reader.ReadInt32();
            MapSegmentCount = reader.ReadInt32();
            Field10 = reader.ReadInt32();
        }
    }

    public class MapEntry
    {
        public long VirtualOffset { get; }
        public long PhysicalOffset { get; }
        public long Size { get; }
        public int Alignment { get; }
        public int StorageType { get; }
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
            StorageType = reader.ReadInt32();
        }
    }
}
